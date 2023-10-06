using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AgileDesign.Utilities;
using Xunit;

namespace UtilitiesFacts
{
    public class NameOfFacts
    {
        [Fact]
        public void NameOfProperty()
        {
            Assert.Equal("SomeProperty", NameOf.Member(() => SomeProperty));
        }

        [Fact]
        public void NameOfParameter()
        {
            MethodWithParameter(null);
        }

        void MethodWithParameter(object testParameter)
        {
            Assert.Equal("testParameter", NameOf.Member(() => testParameter));
        }

        [Fact]
        public void NameOfField()
        {
            Assert.Equal("TestField", NameOf.Member(() => TestField));
        }

        private object TestField;

        [Fact]
        public void NameOfVoidMethod()
        {
            Assert.Equal("NameOfVoidMethod", NameOf.Method(() => NameOfVoidMethod()));
        }

        [Fact]
        public void NameOfMethodWithReturnValue()
        {
            Assert.Equal("SomeFunctionWithoutArgs", NameOf.Method(() => SomeFunctionWithoutArgs()));
            Assert.Equal("SomeFunctionWith1Arg", NameOf.Method(() => SomeFunctionWith1Arg(null)));
            Assert.Equal("SomeFunctionWith2Args", NameOf.Method(() => SomeFunctionWith2Args(null, null)));
        }

        private string SomeProperty { get; set; }

        private string SomeFunctionWithoutArgs()
        {
            return "";
        }
        private NameOfFacts SomeFunctionWith1Arg(object o)
        {
            return null;
        }
        private object SomeFunctionWith2Args(string s, object o)
        {
            return "";
        }
    }
}
