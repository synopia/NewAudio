using System.Collections.Generic;
using System.Linq;
using VL.Lib.Collections;
using Xt;

namespace VL.NewAudio.Core
{
    public class ListDevices
    {
        private IAudioService _audioService = Resources.GetAudioService();
        public bool Input { get; set; }
        public bool Output { get; set; }
        public bool Asio { get; set; }
        public bool Wasapi { get; set; }
        public bool DirectSound { get; set; }

        public string? Default => DeviceNames.FirstOrDefault(d => d.IsDefault)?.Name;
        public Spread<string> Devices => DeviceNames.Select(d=>d.Name).ToSpread();

        public IEnumerable<DeviceName> DeviceNames => _audioService.GetDevices().Where(d => (!Input || d.IsInput)
        && (!Output || d.IsOutput) && (!Asio || d.System==XtSystem.ASIO) && (!Wasapi||d.System==XtSystem.WASAPI) && (!DirectSound ||d.System==XtSystem.DirectSound) );
    }
}