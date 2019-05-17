### VL.NewAudio

Implementation of a modular audio engine for [VL](https://vvvv.org/documentation/vl) written from scratch.

![Sample1](help/additivesynth.png)

There are a few important VL Nodes:

#### WaveInput

Any Windows sound input or capture device (WaveIn, Wasapi, Wasapi Loopback or ASIO). Outputs a fresh AudioSampleBuffer, that can be used later.

#### WaveOutput

Any Windows sound output device (WaveOut, DirectSound, Wasapi or ASIO). Takes an AudioSampleBuffer and sends data to sound driver.

* WaveOut: Do not use this, its freaking slow!
* DirectSound, nah
* Wasapi: This gives good performance and work with low latency (~45m)
* ASIO: Use this if possible, latency is below 20ms

#### AudioSampleBuffer

The data type, that flows through the VL patch. Contains a small portion of sound data in 32 bit float precision. May contain multiple channels in interleaved format (ch0, ch1, ch2, ch0, ch1, ...).

#### AudioSampleLoop

A custom region, that loops over an AudioSampleBuffer in the sound render thread.

In each iteration you have access to an AudioSampleAccessor. Use this with the Get/SetSample Operations to access samples per input/output channel.

If you want to use any time related VL Nodes, connect the supplied clock to your nodes. This clock ticks on sample base.

#### Get/SetSamples

Operations to gain access to samples per channel in AudioSampleLoop.

#### AudioSplitter

Splits one incoming multichannel AudioSampleBuffer into several single channel buffers. 

#### AudioMixer

To mix multiple AudioSampleBuffers into exactly one output Buffer, you can use the AudioMixer.
Takes a Spread of AudioSampleBuffers (with any number of channels) and a Spread of Integers, to map input to output channels.

#### FFT

Provides FFT data in raw format (real and imaginary part). See example for conversion to bins.

#### VCV

For some advance examples, check out Sample.CV for some crazy control voltage like machines.

Currently there are some remakes of a VCO, VCF, LFO and a Delay.

![Sample2](help/vcv.png)

