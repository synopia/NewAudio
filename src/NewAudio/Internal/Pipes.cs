using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace VL.NewAudio.Internal
{
    public class SafeDisposable : IDisposable
    {
        public object DisposeLock = new object();
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            lock (DisposeLock)
            {
                if (!IsDisposed)
                {
                    IsDisposed = true;
                }
            }
        }
        
        protected virtual void DisposeCore() {}

        public void AssertSafe()
        {
            lock (DisposeLock)
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().Name + " has been disposed!");
                }
            }
        }
    }

    public class SafeMemoryMappedFile : SafeDisposable
    {
        private readonly MemoryMappedFile _mmFile;
        private readonly MemoryMappedViewAccessor _accessor;
        private unsafe byte* _pointer;
        
        public int Length { get; private set; }

        public MemoryMappedViewAccessor Accessor
        {
            get{ AssertSafe();
                return _accessor;
            }
        }

        public unsafe byte* Pointer
        {
            get
            {
                AssertSafe();
                return _pointer;
            }
            
        }

        public unsafe SafeMemoryMappedFile(MemoryMappedFile mmFile)
        {
            _mmFile = mmFile;
            _accessor = _mmFile.CreateViewAccessor();
            _pointer = (byte*)_accessor.SafeMemoryMappedViewHandle.DangerousGetHandle().ToPointer();
            Length = (int)_accessor.Capacity;
        }

        protected override unsafe void DisposeCore()
        {
            base.DisposeCore();
            _accessor.Dispose();
            _mmFile.Dispose();
            _pointer = null;
        }
    }

    public abstract class MutexFreePipe : SafeDisposable
    {
        protected const int BufferSize = 1024;
        protected readonly int MessageHeaderLength = sizeof(int);
        protected readonly int StartingOffset = sizeof(int) + sizeof(bool);

        public readonly string Name;
        protected readonly EventWaitHandle NewMessageSignal;
        protected SafeMemoryMappedFile Buffer;
        protected int Offset;
        protected int Length;

        protected MutexFreePipe(string name, bool createBuffer)
        {
            Name = name;
            var mmFile = createBuffer
                ? MemoryMappedFile.CreateNew(name + ".0", BufferSize, MemoryMappedFileAccess.ReadWrite)
                : MemoryMappedFile.OpenExisting(name + ".0");

            Buffer = new SafeMemoryMappedFile(mmFile);
            NewMessageSignal = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".signal");
            Length = Buffer.Length;
            Offset = StartingOffset;
        }

        protected override void DisposeCore()
        {
            base.DisposeCore();
            Buffer.Dispose();
            NewMessageSignal.Dispose();
        }
    }

    public class OutPipe : MutexFreePipe
    {
        private int _messageNumber;
        private int _bufferCount;
        private readonly List<SafeMemoryMappedFile> _oldBuffers = new();
        public int PendingBuffers => _oldBuffers.Count;

        public OutPipe(string name, bool createBuffer) : base(name, createBuffer)
        {
        }

        public unsafe void Write(byte[] data)
        {
            lock (DisposeLock)
            {
                AssertSafe();
                if (data.Length > Length - Offset - 8)
                {
                    WriteContinuation(data.Length);
                }

                WriteMessage(data);
                NewMessageSignal.Set();
            }
        }

        unsafe void WriteMessage(byte[] block)
        {
            byte* ptr = Buffer.Pointer;
            byte* offsetPointer = ptr + Offset;
            var msgPointer = (int*)offsetPointer;
            *msgPointer = block.Length;
            Offset += MessageHeaderLength;
            offsetPointer += MessageHeaderLength;
            if (block.Length > 0)
            {
                Marshal.Copy(block, 0, new IntPtr(offsetPointer), block.Length);
                Offset += block.Length;
            }

            int* iptr = (int*)ptr;
            *iptr = ++_messageNumber;
        }

        void WriteContinuation(int messageSize)
        {
            string newName = Name + "." + ++_bufferCount;
            int newLength = BufferSize;
            var newFile =
                new SafeMemoryMappedFile(MemoryMappedFile.CreateNew(newName, newLength,
                    MemoryMappedFileAccess.ReadWrite));
            WriteMessage(Array.Empty<byte>());
            _oldBuffers.Add(Buffer);
            Buffer = newFile;
            Length = newFile.Length;
            Offset = StartingOffset;

            foreach (var oldBuffer in _oldBuffers.Take(_oldBuffers.Count-1).ToArray())
            {
                lock (DisposeLock)
                {
                    if (oldBuffer.Accessor.ReadBoolean(4))
                    {
                        _oldBuffers.Remove(oldBuffer);
                        oldBuffer.Dispose();
                    }
                }
            }
        }

        protected override void DisposeCore()
        {
            base.DisposeCore();
            foreach (var oldBuffer in _oldBuffers)
            {
                oldBuffer.Dispose();
            }
        }
    }

    public class InPipe : MutexFreePipe
    {
        private int _lastMessageProcessed;
        private int _bufferCount;
        private readonly Action<byte[]> _onMessage;

        public InPipe(string name, bool createBuffer, Action<byte[]> onMessage) : base(name, createBuffer)
        {
            _onMessage = onMessage;
            new Thread(Go).Start();
        }

        void Go()
        {
            int spinCycles = 0;
            while (true)
            {
                int? latestMessageId = GetLatestMessageId();
                if (latestMessageId == null)
                {
                    return;
                }

                if (latestMessageId > _lastMessageProcessed)
                {
                    Thread.MemoryBarrier();
                    byte[] msg = GetNextMessage();
                    if (msg == null)
                    {
                        return;
                    }

                    if (msg.Length > 0 && _onMessage != null)
                    {
                        _onMessage(msg);
                    }

                    spinCycles = 1000;
                }

                if (spinCycles == 0)
                {
                    NewMessageSignal.WaitOne();
                    if (IsDisposed)
                    {
                        return;
                    }
                }
                else
                {
                    Thread.MemoryBarrier();
                    spinCycles--;
                }

            }
        }

        unsafe int? GetLatestMessageId()
        {
            lock(DisposeLock)
            lock (Buffer.DisposeLock)
            {
                return IsDisposed || Buffer.IsDisposed ? null : *(int*)Buffer.Pointer;
            }
        }

        unsafe byte[] GetNextMessage()
        {
            _lastMessageProcessed++;
            lock (DisposeLock)
            {
                if (IsDisposed)
                {
                    return null;
                }

                lock (Buffer.DisposeLock)
                {
                    if (Buffer.IsDisposed)
                    {
                        return null;
                    }

                    byte* offsetPointer = Buffer.Pointer + Offset;
                    var msgPointer = (int*)offsetPointer;
                    int msgLength = *msgPointer;
                    Offset += MessageHeaderLength;
                    offsetPointer += MessageHeaderLength;
                    if (msgLength == 0)
                    {
                        Buffer.Accessor.Write(4, true);
                        Buffer.Dispose();
                        string newName = Name + "." + ++_bufferCount;
                        Buffer = new SafeMemoryMappedFile(MemoryMappedFile.OpenExisting(newName));
                        Offset = StartingOffset;
                        return Array.Empty<byte>();
                    }

                    Offset += msgLength;
                    var msg = new byte[msgLength];
                    Marshal.Copy(new IntPtr(offsetPointer), msg, 0, msgLength);
                    return msg;
                }
            }
        }

        protected override void DisposeCore()
        {
            NewMessageSignal.Set();
            base.DisposeCore();
        }
    }
}