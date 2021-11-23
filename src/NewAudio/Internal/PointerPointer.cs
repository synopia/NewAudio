using System;
using System.Runtime.InteropServices;
using Xt;

namespace NewAudio.Internal
{
    public sealed class PointerPointer: IDisposable
    {
        private int _outerLength;
        private int _innerLength;
        GCHandle[] ppGch = null;
        IntPtr[] pp = null;
        GCHandle gch;
        private Type _type;
        private Array _data;

        public Array Data => _data;
        public IntPtr Pointer => gch.AddrOfPinnedObject();

        public PointerPointer(Type type, int outerLength, int innerLength)
        {
            _type = type;
            _outerLength = outerLength;
            _innerLength = innerLength;
            Alloc();
        }

        private void Alloc()
        {
            ppGch = new GCHandle[_outerLength];
            pp = new IntPtr[_outerLength];
            _data = Array.CreateInstance(_type.MakeArrayType(), _outerLength);
            for (int i = 0; i < _outerLength; i++)
            {
                var inner = Array.CreateInstance(_type, _innerLength);
                _data.SetValue(inner, i);
                ppGch[i] = GCHandle.Alloc(inner, GCHandleType.Pinned);
                pp[i] = ppGch[i].AddrOfPinnedObject();
            }
            gch = GCHandle.Alloc(pp, GCHandleType.Pinned);

        }

        public void Dispose()
        {
            if (ppGch != null)
            {
                for (int i = 0; i < _outerLength; i++)
                {
                    ppGch[i].Free();
                }
            }

            gch.Free();

        }
    }
}