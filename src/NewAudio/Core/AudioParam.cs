using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;

namespace NewAudio.Core
{
  
    public interface IAudioParam
    {
        object ObjValue { get; set; }
        object LastObjValue { get; }
        object NextObjValue { get; }
        bool HasChanged { get; }
        void Reset();
        void Commit();
    }
    public interface IAudioParam<T> : IAudioParam 
    {
        T Value { get; set; }
        T LastValue { get; }
        T NextValue { get; }
    }

    internal class AudioParamsLastValue<T> : RealProxy
    {
        private Dictionary<string, IAudioParam> _props;

        public AudioParamsLastValue(Dictionary<string, IAudioParam> props):base(typeof(T))
        {
            _props = props;
        }

        public override IMessage Invoke(IMessage msg)
        {
            var methodCall = (IMethodCallMessage)msg;
            var method = (MethodInfo)methodCall.MethodBase;
            if (method.Name.StartsWith("get"))
            {
                return new ReturnMessage(_props[method.Name.Split('_')[1]].LastObjValue, null, 0, methodCall.LogicalCallContext, methodCall);                
            } 

            throw new Exception("Something went wrong");
        }
    }
    internal class AudioParamsNextValue<T> : RealProxy
    {
        private Dictionary<string, IAudioParam> _props;

        public AudioParamsNextValue(Dictionary<string, IAudioParam> props):base(typeof(T))
        {
            _props = props;
        }

        public override IMessage Invoke(IMessage msg)
        {
            var methodCall = (IMethodCallMessage)msg;
            var method = (MethodInfo)methodCall.MethodBase;
            if (method.Name.StartsWith("get"))
            {
                return new ReturnMessage(_props[method.Name.Split('_')[1]].NextObjValue, null, 0, methodCall.LogicalCallContext, methodCall);                
            } 

            throw new Exception("Something went wrong");
        }
    }
    internal class AudioParamsGetSet<T> : RealProxy
    {
        private Dictionary<string, IAudioParam> _props;

        public AudioParamsGetSet(Dictionary<string, IAudioParam> props):base(typeof(T))
        {
            _props = props;
        }
        bool HasChanged => _props.Any(p => p.Value.HasChanged);

        public override IMessage Invoke(IMessage msg)
        {
            var methodCall = (IMethodCallMessage)msg;
            var method = (MethodInfo)methodCall.MethodBase;
            if (method.Name == "get_HasChanged")
            {
                return new ReturnMessage(HasChanged, null, 0, methodCall.LogicalCallContext, methodCall);
            }
            if (method.Name.StartsWith("get"))
            {
                return new ReturnMessage(_props[method.Name.Split('_')[1]].ObjValue, null, 0, methodCall.LogicalCallContext, methodCall);                
            } 
            if (method.Name.StartsWith("set"))
            {
                _props[method.Name.Split('_')[1]].ObjValue = methodCall.Args[0];
                return new ReturnMessage(null, null, 0, methodCall.LogicalCallContext, methodCall);                
            }

            throw new Exception("Something went wrong");
        }
    } 
    
    public class AudioParams
    {
        private Dictionary<string, IAudioParam> _props = new Dictionary<string, IAudioParam>();
        public Action OnCommit;
        public AudioParams(Type type)
        {
            var paramType = typeof(AudioParam<>);
            foreach (var prop in (new Type[] { type })
                .Concat(type.GetInterfaces())
                .SelectMany(i => i.GetProperties()))
            {
                var rType = prop.PropertyType;
                var finalType = paramType.MakeGenericType(rType);
                var instance = Activator.CreateInstance(finalType);
                _props[prop.Name] = (IAudioParam)instance;
            }
        }

        public T Create<T>()
        {
            return  (T)new AudioParamsGetSet<T>(_props).GetTransparentProxy();
        }
        public T CreateLast<T>()
        {
            return  (T)new AudioParamsLastValue<T>(_props).GetTransparentProxy();
        }
        public T CreateNext<T>()
        {
            return  (T)new AudioParamsNextValue<T>(_props).GetTransparentProxy();
        }

        public AudioParam<T2> Get<T2>(string name)
        {
            return (AudioParam<T2>)_props[name];
        }
        
        public bool HasChanged => _props.Any(p => p.Value.HasChanged);

        public void Reset()
        {
            foreach (var param in _props)
            {
                param.Value.Reset();
            }
        }

        public void Commit()
        {
            if (HasChanged)
            {
                foreach (var param in _props)
                {
                    param.Value.Commit();
                }
                OnCommit?.Invoke();
            }
        }
    }
    
    public class AudioParam<T>  : IAudioParam<T>
    {
        private T _currentValue;
        
        private T _lastValue;
        private T _newValue;
        public Action<T> OnReset;
        public Action<T, T> OnCommit;

        public AudioParam():this(default)
        {
        }
        public AudioParam(T currentValue)
        {
            _currentValue = currentValue;
            _lastValue = default;
            _newValue = currentValue;
        }

        public object ObjValue
        {
            get => _currentValue;
            set => _newValue = (T)value;
        }
        public T Value
        {
            get => _currentValue;
            set => _newValue = value;
        }
        public T NextValue
        {
            get => _newValue;
            private set => throw new NotImplementedException();
        }

        public T LastValue => _lastValue;
        public object LastObjValue => _lastValue;
        public object NextObjValue => _newValue;
        
        public bool HasChanged => !Equals(_currentValue, _newValue);

        public void Reset()
        {
            _newValue = _currentValue;
            OnReset?.Invoke(_currentValue);
        }

        public void Commit()
        {
            if (HasChanged)
            {
                _lastValue = _currentValue;
                _currentValue = _newValue;
                OnCommit?.Invoke(_lastValue, _currentValue);
            }
        }

    }
    public class AudioListParam<T, TX>  : IAudioParam<T> where T : IEnumerable<TX> 
    {
        private T _currentValue;
        
        private T _lastValue;
        private T _newValue;

        public AudioListParam():this(default)
        {
            
        }
        public AudioListParam(T currentValue=default)
        {
            _currentValue = currentValue;
            _lastValue = default;
            _newValue = currentValue;
        }

        public object ObjValue
        {
            get => _currentValue;
            set => _newValue = (T)value;
        }
        public T Value
        {
            get => _currentValue;
            set => _newValue = value;
        }
        public T NextValue
        {
            get => _currentValue;
            set => throw new Exception();
        }

        public T LastValue => _lastValue;
        public object LastObjValue => _lastValue;
        public object NextObjValue => _newValue;
        
        public bool HasChanged => !Enumerable.SequenceEqual(_currentValue, _newValue);

        public void Reset()
        {
            _newValue = _currentValue;
        }

        public void Commit()
        {
            if (HasChanged)
            {
                _lastValue = _currentValue;
                _currentValue = _newValue;
            }
        }

    }
}