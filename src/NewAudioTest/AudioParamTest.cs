using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using NUnit.Framework;
using NewAudio.Core;
using NewAudio.Nodes;

namespace NewAudioTest
{
    [TestFixture]
    public class AudioParamTest
    {
        public interface IInterface : IAudioNodeConfig
        {
            int Aint { get; set;  }
            string Astring { get; set; }
        }
        public class Tester : IAudioNode<IInterface>
        {
            private AudioNode _support;

            public AudioParams AudioParams => _support.AudioParams;
            public IInterface Config => _support.Config;
            public IInterface LastConfig => _support.LastConfig;
            public AudioLink Output => _support.Output;
            public bool DoCheck = false;
            public Tester()
            {
                _support = new AudioNode(this);
            }

            public void Update()
            {
                _support.Update();
            }

            public void Dispose()
            {
                
            }

            public bool IsInputValid(IInterface next)
            {
                return !DoCheck || next.Aint == 123;
            }

            public void OnAnyChange()
            {
            }

            public void OnConnect(AudioLink input)
            {
            }

            public void OnDisconnect(AudioLink link)
            {
            }

            public void OnStart()
            {
            }

            public void OnStop()
            {
            }
        }
        [Test]
        public void TestClass()
        {
            
            var t = new Tester();
            Assert.IsFalse(t.Config.HasChanged);

            t.Config.Aint = 10;
            Assert.IsTrue(t.Config.HasChanged);
            t.AudioParams.Commit();
            Assert.IsFalse(t.Config.HasChanged);
            
            Assert.AreEqual(10, t.Config.Aint);
            t.Config.Astring = "Hello";
            Assert.IsTrue(t.Config.HasChanged);
            Assert.IsNull(t.AudioParams.Get<string>("Astring").LastValue);

            t.Config.Aint = 11;
            t.AudioParams.Commit();
            
            Assert.AreEqual(10, t.LastConfig.Aint);

            t.Config.Phase = LifecyclePhase.Playing;
            t.AudioParams.Commit();
            Assert.AreEqual(LifecyclePhase.Playing, t.Config.Phase);

            t.DoCheck = true;
            t.Config.Phase = LifecyclePhase.Finished;
            t.Update();
            Assert.AreEqual(LifecyclePhase.Playing, t.Config.Phase);

            t.Config.Phase = LifecyclePhase.Finished;
            t.Config.Aint = 123;
            t.Update();
            Assert.AreEqual(LifecyclePhase.Finished, t.Config.Phase);
        }
        
        [Test]
        public void TestChangedInt()
        {
            var p1 = new AudioParam<int>(10);
            Assert.AreEqual(0, p1.LastValue);
            p1.Value = 11;
            Assert.IsTrue(p1.HasChanged);
            Assert.AreEqual(10, p1.Value);
            Assert.AreEqual(0, p1.LastValue);
            p1.Reset();
            Assert.IsFalse(p1.HasChanged);
            Assert.AreEqual(10, p1.Value);
            Assert.AreEqual(0, p1.LastValue);
            p1.Value = 12;
            Assert.IsTrue(p1.HasChanged);
            Assert.AreEqual(10, p1.Value);
            Assert.AreEqual(0, p1.LastValue);
            p1.Value = 10;
            Assert.IsFalse(p1.HasChanged);
            Assert.AreEqual(10, p1.Value);
            Assert.AreEqual(0, p1.LastValue);
            p1.Value = 12;
            p1.Commit();
            Assert.IsFalse(p1.HasChanged);
            Assert.AreEqual(12, p1.Value);
            Assert.AreEqual(10, p1.LastValue);
        }

        [Test]
        public void TestChangedList()
        {
            var list1 = new[] { 1, 2, 3 };
            var list2 = new List<int>(new[] { 1, 2, 3 });
            
            var p1 = new AudioListParam<IEnumerable<int>, int>(list1);
            Assert.AreEqual(null, p1.LastValue);
            p1.Value = list2;
            Assert.IsFalse(p1.HasChanged);
            p1.Value = new []{2,3,4};
            Assert.IsTrue(p1.HasChanged);
            Assert.AreEqual(list1, p1.Value);
            Assert.AreEqual(null, p1.LastValue);
        }
    }
}