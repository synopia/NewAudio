using System;
using System.Threading;
using NUnit.Framework;
using Xt;

namespace VL.NewAudioTest
{
    [Apartment(ApartmentState.STA)]
    public class XtTest
    {
        static void OnError(string message)
            => Console.WriteLine(message);

        static void PrintDevices(XtService service, XtDeviceList list)
        {
            for (int d = 0; d < list.GetCount(); d++)
            {
                string id = list.GetId(d);
                try
                {
                    using XtDevice device = service.OpenDevice(id);
                    XtMix? mix = device.GetMix();
                    Console.WriteLine("    Device " + id + ":");
                    Console.WriteLine("      Name: " + list.GetName(id));
                    Console.WriteLine("      Capabilities: " + list.GetCapabilities(id));
                    Console.WriteLine("      Input channels: " + device.GetChannelCount(false));
                    Console.WriteLine("      Output channels: " + device.GetChannelCount(true));
                    Console.WriteLine("      Interleaved access: " + device.SupportsAccess(true));
                    Console.WriteLine("      Non-interleaved access: " + device.SupportsAccess(false));
                    if (mix != null) Console.WriteLine("      Current mix: " + mix.Value.rate + " " + mix.Value.sample);
                } catch (XtException e)
                { Console.WriteLine(XtAudio.GetErrorInfo(e.GetError())); }
            }
        }
        public void Test()
        {
          XtAudio.SetOnError(OnError);
          using XtPlatform platform = XtAudio.Init("X", IntPtr.Zero);
            try
            {
                XtVersion version = XtAudio.GetVersion();
                Console.WriteLine("Version: " + version.major + "." + version.minor);
                XtSystem[] systems = new[] { XtSystem.ASIO, XtSystem.WASAPI, XtSystem.DirectSound }; 
                foreach (XtSystem s in systems)
                {
                    XtService service = platform.GetService(s);
                    using XtDeviceList all = service.OpenDeviceList(XtEnumFlags.All);
                    Console.WriteLine("System: " + s);
                    Console.WriteLine("  Capabilities: " + service.GetCapabilities());
                    string defaultInput = service.GetDefaultDeviceId(false);
                    if (defaultInput != null)
                    {
                        string name = all.GetName(defaultInput);
                        Console.WriteLine("  Default input: " + name + " (" + defaultInput + ")");
                    }
                    string defaultOutput = service.GetDefaultDeviceId(true);
                    if (defaultOutput != null)
                    {
                        string name = all.GetName(defaultOutput);
                        Console.WriteLine("  Default output: " + name + " (" + defaultOutput + ")");
                    }
                    using XtDeviceList inputs = service.OpenDeviceList(XtEnumFlags.Input);
                    Console.WriteLine("  Input device count: " + inputs.GetCount());
                    PrintDevices(service, inputs);
                    using XtDeviceList outputs = service.OpenDeviceList(XtEnumFlags.Output);
                    Console.WriteLine("  Output device count: " + outputs.GetCount());
                    PrintDevices(service, outputs);
                }
            } catch (XtException e)
            { Console.WriteLine(XtAudio.GetErrorInfo(e.GetError()));
            } catch (Exception e)
            { Console.WriteLine(e.Message); }
        }

        public void Test1()
        {
          
        }
    }
}