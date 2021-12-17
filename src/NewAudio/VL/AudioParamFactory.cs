using System;
using VL.Core;

namespace VL.NewAudio.Core
{

    public class AudioParamBuilder<T> : IMonadBuilder<T, AudioParam<T>>
    {
        private readonly AudioParam<T> _param;
        public AudioParamBuilder()
        {
            _param = new AudioParam<T>();
        }

        public AudioParam<T> Return(T value)
        {
            _param.Value = value;
            return _param;
        }
    }
    public class AudioParamFactory<T>: IMonadicFactory<T, AudioParam<T>> 
    {
        public static readonly AudioParamFactory<T> Default = new();

        public IMonadBuilder<T, AudioParam<T>> GetMonadBuilder(bool isConstant)
        {
            if (typeof(T)!=typeof(float))
            {
                throw new NotImplementedException();
            }

            return (IMonadBuilder<T, AudioParam<T>>)Activator.CreateInstance(typeof(AudioParamBuilder<>).MakeGenericType(typeof(T)));
        }
    }
}