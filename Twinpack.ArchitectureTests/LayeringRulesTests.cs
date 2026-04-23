using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetArchTest.Rules;
using ArchTestResult = NetArchTest.Rules.TestResult;
using Twinpack.Protocol.Api;

namespace Twinpack.ArchitectureTests;

[TestClass]
public class LayeringRulesTests
{
    [TestMethod]
    public void Protocol_Api_types_should_not_depend_on_Core_automation_layer()
    {
        var result = Types.InAssembly(typeof(PublishedPackage).Assembly)
            .That().ResideInNamespace("Twinpack.Protocol.Api")
            .ShouldNot().HaveDependencyOn("Twinpack.Core")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful, FormatFailures(result));
    }

    [TestMethod]
    public void Protocol_Api_types_should_not_depend_on_Configuration_layer()
    {
        var result = Types.InAssembly(typeof(PublishedPackage).Assembly)
            .That().ResideInNamespace("Twinpack.Protocol.Api")
            .ShouldNot().HaveDependencyOn("Twinpack.Configuration")
            .GetResult();

        Assert.IsTrue(result.IsSuccessful, FormatFailures(result));
    }

    private static string FormatFailures(ArchTestResult result)
    {
        if (result.FailingTypes == null || !result.FailingTypes.Any())
            return result.ToString() ?? string.Empty;

        var sb = new StringBuilder();
        foreach (var t in result.FailingTypes)
            sb.AppendLine(t.FullName);
        return sb.ToString();
    }
}
