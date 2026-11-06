using Microsoft.VisualStudio.TestTools.UnitTesting;
using Spectre.Console.Cli;
using System.Linq;
using System.Reflection;
using Twinpack.Commands;

namespace TwinpackTests
{
    [TestClass]
    public class NoDependenciesOptionTest
    {
        [TestMethod]
        public void AddAndUpdate_ExposeNoDependencies()
        {
            AssertOption(typeof(AddCommand.Settings));
            AssertOption(typeof(UpdateCommand.Settings));
        }

        static void AssertOption(System.Type settings)
        {
            var property = settings.GetProperty("NoDependencies");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(bool), property.PropertyType);

            var option = property.GetCustomAttribute<CommandOptionAttribute>();
            Assert.IsNotNull(option);
            CollectionAssert.Contains(option.LongNames.ToList(), "no-dependencies");
        }
    }
}
