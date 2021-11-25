using System.Collections.Generic;
using System.Linq;
using NewAudio.Core;
using NUnit.Framework;
using Xt;

namespace NewAudioTest
{
    
    public class BaseBlockTest : BaseTest
    {
        [SetUp]
        public void Clear()
        {
        }

        protected virtual IList<TestDevice> Devices()
        {
            return new List<TestDevice>();
        }

        protected override IXtPlatform CreatePlatform()
        {
            return new TestPlatform(Devices());
        }

        [TearDown]
        public void Done()
        {
            
        }
    }
}