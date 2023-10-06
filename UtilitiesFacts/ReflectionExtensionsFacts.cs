using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AgileDesign.Utilities;
using Xunit;

namespace Utilities.UnitTests
{
    [Serializable] //SerializableAttibute is needed for HasTypeLevelAttribute unit test only, not for serialization
    public class ReflectionExtensionsFacts
    {
        [Fact]
        public void GetCustomAssembliesFiltersOutFrameworkAssemblies()
        {
            var filteredAssemblies = AppDomain.CurrentDomain.GetCustomAssemblies();

            var filteredTokens = filteredAssemblies.Select(a => a.GetName().GetPublicKeyToken());
            foreach (var msPublicToken in msPublicTokens)
            {
                Assert.DoesNotContain(msPublicToken, filteredTokens, new ByteArrayEqualityComparer());
            }

            foreach (var assemblyName 
                in filteredAssemblies.Select(a => a.GetName()))
            {
                Console.WriteLine(assemblyName.FullName);
            }
            
        }
        byte[][] msPublicTokens = new []
        {
            new byte[] {0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89},
            new byte[] {0x31, 0xbf, 0x38, 0x56, 0xad, 0x36, 0x4e, 0x35},
            new byte[] {0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a}
        };

        [Fact]
        public void HasTypeLevelAttribute()
        {
            Assert.True(typeof(ReflectionExtensionsFacts)
                .Name.HasTypeLevelAttribute<SerializableAttribute>());

            Assert.False(typeof(ReflectionExtensionsFacts)
                .Name.HasTypeLevelAttribute<ObsoleteAttribute>());

            Assert.Throws<ArgumentException>(
                () => "NotExstentClass".HasTypeLevelAttribute<SerializableAttribute>());
        }
    }
}
