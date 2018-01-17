using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace FG.Utils.BuildTools.Tests
{
    public class ProjectTool_tests
    {
        private const string ClassicProjectRelativePath =
            @"../../../../../samples/Ce.Labs.Samples.ClassicProject/Ce.Labs.Samples.ClassicProject.csproj";
        private const string CPSProjectRelativePath =
            @"../../../../../samples/Ce.Labs.Samples.CPSProject/Ce.Labs.Samples.CPSProject.csproj";
        private ProjectTool GetProjectTool(string projectRelativePath)
        {
            var currentFolder = System.IO.Path.GetDirectoryName(new Uri(this.GetType().Assembly.CodeBase).LocalPath);
            var csProjAbsolutePath = PathExtensions.GetAbsolutePath(currentFolder, projectRelativePath);

            var projectTool = new ProjectTool(csProjAbsolutePath, new DebugLogger(true));
            return projectTool;
        }

        private void Should_be_able_to_load_files(string projectRelativePath)
        {
            var projectTool = GetProjectTool(projectRelativePath);
            var scanedFiles = projectTool.ScanFilesInProjectFolder();

            var files = scanedFiles
                .Where(f => f.IncludeType != "Remove")
                .Where(f => f.IncludeType != "Unknown")
                .Select(f => $"{f.IncludeType}:{f.Name}")
                .Where(f => !f.EndsWith("Properties\\AssemblyInfo.cs"))
                .ToArray();

            files.Debug(new ConsoleDebugLogger(true));

            files.OrderBy(o => o).ShouldBeEquivalentTo(new[]
            {
                "Compile:SampleClass1IncludedInproject.cs",
                "Compile:SampleClass4IncludedInprojectWithProperties.cs",
                "Compile:SampleClass5IncludedInprojectDependentOnClass4.cs",
                "Compile:SubFolder\\SampleClass2IncludedInproject.cs",
                "None:configurationFile.config",
                "None:includedFile.json",
            }.OrderBy(o => o));            
        }

        private void Should_be_able_to_load_file_properties(string projectRelativePath)
        {
            var projectTool = GetProjectTool(projectRelativePath);
            var scanedFiles = projectTool.ScanFilesInProjectFolder();

            var fileWithProperties = scanedFiles
                .Single(f => f.Name == "SampleClass4IncludedInprojectWithProperties.cs");

            fileWithProperties.Properties["Property1"].Should().Be("sample");
            fileWithProperties.Properties["Property2"].Should().Be("class4");

            var fileWithoutProperties = scanedFiles
                .Single(f => f.Name == "SampleClass1IncludedInproject.cs");

            fileWithoutProperties.Properties.Should().HaveCount(0);
        }

        private void Should_be_able_to_load_project_properties(string projectRelativePath, params string[] projectTypeUniqueChecks)
        {
            var projectTool = GetProjectTool(projectRelativePath);
            var projectProperties = projectTool.GetProjectProperties("Debug", "AnyCPU");

            var properties = projectProperties
                .Where(kv => kv.Key != "Configuration")
                .Where(kv => kv.Key != "Platform")
                .Select(kv => $"{kv.Key}:{kv.Value}")
                .OrderBy(o => o)
                .Debug(new ConsoleDebugLogger(true))
                .ToArray();            

            properties.Should().BeEquivalentTo(new[]
                {
                    "RootNamespace:Ce.Labs.Samples",
                    "OutputType:Library",
                }.Union(projectTypeUniqueChecks)
                .OrderBy(o => o)
                .Debug(new ConsoleDebugLogger(true))
                .ToArray()
                );
        }

        [Test]
        public void Should_be_able_to_load_files_in_classic_project()
        {
            Should_be_able_to_load_files(ClassicProjectRelativePath);
        }

        [Test]
        public void Should_be_able_to_load_files_in_cps_project()
        {
            Should_be_able_to_load_files(CPSProjectRelativePath);
        }

        [Test]
        public void Should_be_able_to_load_file_properties_in_classic_project()
        {
            Should_be_able_to_load_file_properties(ClassicProjectRelativePath);
        }

        [Test]
        public void Should_be_able_to_load_file_properties_in_cps_project()
        {
            Should_be_able_to_load_file_properties(CPSProjectRelativePath);
        }

        [Test]
        public void Should_be_able_to_load_project_properties_in_classic_project()
        {
            Should_be_able_to_load_project_properties(ClassicProjectRelativePath,
                "AssemblyName:Ce.Labs.Samples.ClassicProject",
                "TargetFrameworkVersion:v4.6.1", 
                "AppDesignerFolder:Properties",
                "ProjectGuid:{8976B123-80BD-459A-BB2B-46DE19140EA2}", 
                "FileAlignment:512",
                "OutputPath:bin\\Debug\\special_bin\\",
                "DebugSymbols:true",
                "DebugType:full",
                "DefineConstants:DEBUG;TRACE",
                "ErrorReport:prompt",
                "Optimize:false",
                "WarningLevel:4");
        }

        [Test]
        public void Should_be_able_to_load_project_properties_in_cps_project()
        {
            Should_be_able_to_load_project_properties(CPSProjectRelativePath,
                "AssemblyName:Ce.Labs.Samples.CPSProject",
                "Authors:Code Effect", 
                "Company:Code Effect",
                "Product:CE Labs",
                "Description:Sample project for running testing CE Labs BuildTools",
                "Platforms:AnyCPU;x64",
                "SkipValidatePackageReferences:true",
                "RuntimeIdentifier:win7-x64",
                "TargetFramework:net461",
                "OutputPath:bin\\Debug\\net461\\win7-x64\\special_bin");
        }
    }
}