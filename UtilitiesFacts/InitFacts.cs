using System;
using System.Diagnostics.Contracts;
using System.Threading;
using AgileDesign.Utilities;
using Xunit;

namespace UtilitiesFacts
{
    public class InitFacts
    {
        static object locker = new object();
        static RaceConditionTester fieldToInit;
        static RaceConditionTester FieldToInit
        {
            get { return Init.InitIfNull(ref fieldToInit); }
        }

        object simpleField;
        [Fact]
        public void InitIfNullExtensionMethod()
        {
            Assert.Null(simpleField);
            //Creates new instance if null
            Assert.NotNull(Init.InitIfNull(ref simpleField));
            Assert.NotNull(simpleField);

            //Does not override an existing value if that is not null
            var newValue = new object();
            simpleField = newValue;
            Assert.Equal(newValue, Init.InitIfNull(ref simpleField));
            Assert.Equal(newValue, simpleField);
        }

        [Fact]
        public void InitIfNullDoesNotOverrideAssignedField()
        {
            fieldToInit = new RaceConditionTester();
            fieldToInit.instanceId = 42;
            Init.InitIfNullLocking(ref fieldToInit, locker);

            Assert.NotNull(fieldToInit);
            Assert.Equal(42, fieldToInit.instanceId);
        }

        [Fact]
        public void InitIfNullCreatesInstanceInRaceCondition()
        {
            Action initIfNullAction = () => Init.InitIfNullLocking(ref fieldToInit, locker);

            initIfNullAction.BeginInvoke(null, null);
            initIfNullAction.BeginInvoke(null, null);
            initIfNullAction.BeginInvoke(null, null);

            for (int i = 0; i < 10; i++)
            {
                if (fieldToInit != null)
                    break;

                Thread.Sleep(10);
            }
            Assert.NotNull(fieldToInit);
            Assert.Equal(1, RaceConditionTester.lockedByInstanceId);
        }

        private class RaceConditionTester
        {
            public static int lockedByInstanceId;
            public int instanceId;

            public RaceConditionTester()
            {
                lockedByInstanceId++;
                instanceId = lockedByInstanceId;
                int waitMiliseconds = 10 - instanceId;
                Thread.Sleep(waitMiliseconds);
            }
        }

    }
}
