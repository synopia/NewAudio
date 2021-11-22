using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NewAudio.Core
{
    public interface IAudioParam
    {
        object Value { get; set; }
        object LastValue { get; }
        public bool HasChanged { get; }

        public void Commit();
        public void Rollback();
        Func<Task> OnChange { get; set; }
    }

    public interface IAudioParam<T>
    {
        T Value { get; set; }
        T LastValue { get; }
        public bool HasChanged { get; }
    }

    public class AudioParam<T> : IAudioParam<T>, IAudioParam
    {
        private T _currentValue;
        private T _lastValue;

        public T Value
        {
            get => _currentValue;
            set => _currentValue = value;
        }

        object IAudioParam.Value
        {
            get => _currentValue;
            set => _currentValue = (T)value;
        }

        public bool HasChanged => !Equals(_lastValue, _currentValue);
        public T LastValue => _lastValue;
        object IAudioParam.LastValue => _lastValue;

        public Func<Task> OnChange { get; set; }

        public void Commit()
        {
            _lastValue = _currentValue;
        }

        public void Rollback()
        {
            _currentValue = _lastValue;
        }
    }

    public abstract class AudioParams
    {
        public readonly Dictionary<string, IAudioParam> Params = new();

        protected AudioParams()
        {
            var props = GetType().GetFields();
            foreach (var prop in props)
            {
                var type = prop.FieldType;
                if (typeof(IAudioParam).IsAssignableFrom(type))
                {
                    var instance = Activator.CreateInstance(type);
                    prop.SetValue(this, instance);
                    Params[prop.Name] = (IAudioParam)instance;
                }
            }
        }

        public bool HasChanged => Params.Values.Any(p => p.HasChanged);
        public IEnumerable<IAudioParam> ChangedValues => Params.Values.Where(p => p.HasChanged);
        public Action OnChange;

        public static T Create<T>()
        {
            return (T)Activator.CreateInstance(typeof(T));
        }
        
        public void AddGroupOnChange(IEnumerable<IAudioParam> param, Func<Task> action)
        {
            foreach (var p in param)
            {
                p.OnChange += action;
            }
        }

        public void Update()
        {
            var delegates = new List<Delegate>();

            foreach (var param in Params.Values)
            {
                if (param.HasChanged)
                {
                    var list = param.OnChange?.GetInvocationList();
                    if (list != null)
                    {
                        delegates.AddRange(list);
                    }
                }
            }

            Commit();

            foreach (var @delegate in delegates.Distinct())
            {
                @delegate.DynamicInvoke();
            }

            if (delegates.Count > 0)
            {
                if (OnChange != null)
                {
                    OnChange.Invoke();
                }
            }
        }

        public void Commit()
        {
            foreach (var param in Params.Values)
            {
                param.Commit();
            }
        }

        public void Rollback()
        {
            foreach (var param in Params.Values)
            {
                param.Rollback();
            }
        }
    }
}