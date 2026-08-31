using System.IO;
using NUnit.Framework;

namespace RuriKit.Tests.EditMode
{
    /// <summary>
    ///     验证测试程序集与 Runtime 程序集彼此隔离的基本包结构。
    /// </summary>
    public class AssemblyShapeTests
    {
        /// <summary>
        ///     验证测试文件位于 Tests 目录，不会进入 Runtime 程序集。
        /// </summary>
        [Test]
        public void TestAssembly_WhenLocatedInTestsDirectory_ShouldNotBeInsideRuntimeDirectory()
        {
            string path = typeof(AssemblyShapeTests).Assembly.Location.Replace('\\', '/');

            Assert.That(path, Does.Not.Contain("/Runtime/"));
            Assert.That(Path.GetFileName(path), Does.Contain("RuriKit.Tests.EditMode"));
        }
    }
}
