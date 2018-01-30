﻿using System;
using System.Collections.Generic;
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

        private const string CPSProjectRelativePathNoOutputPath =
            @"../../../../../samples/Ce.Labs.Samples.CPSProjectNoOutputPath/Ce.Labs.Samples.CPSProjectNoOutputPath.csproj";
        private const string CPSProjectRelativePathNoOutputPathNoRuntime =
            @"../../../../../samples/Ce.Labs.Samples.CPSProjectNoOutputPathNoRuntime/Ce.Labs.Samples.CPSProjectNoOutputPathNoRuntime.csproj";
        private const string CPSProjectRelativePathNoOutputPathx64 =
            @"../../../../../samples/Ce.Labs.Samples.CPSProjectNoOutputPathx64/Ce.Labs.Samples.CPSProjectNoOutputPathx64.csproj";
        private const string CPSProjectRelativePathNoOutputPathx64NoRuntime =
            @"../../../../../samples/Ce.Labs.Samples.CPSProjectNoOutputPathx64NoRuntime/Ce.Labs.Samples.CPSProjectNoOutputPathx64NoRuntime.csproj";


        private readonly IList<string> _temporaryProjectFileCopies = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (var temporaryProjectFileCopy in _temporaryProjectFileCopies)
            {
                System.IO.File.Delete(temporaryProjectFileCopy);
            }
        }

        private ProjectTool TempCopyAndGetProject(string projectRelativePath)
        {
            var currentFolder = System.IO.Path.GetDirectoryName(new Uri(this.GetType().Assembly.CodeBase).LocalPath);
            var csProjAbsolutePath = PathExtensions.GetAbsolutePath(currentFolder, projectRelativePath);

            var projectFolder = System.IO.Path.GetDirectoryName(csProjAbsolutePath);
            var projectTempCopyName = $"{System.IO.Path.GetRandomFileName()}.csproj";
            var projectTempCopyPath = PathExtensions.GetAbsolutePath(projectFolder, projectTempCopyName);

            System.IO.File.Copy(csProjAbsolutePath, projectTempCopyPath);

            _temporaryProjectFileCopies.Add(projectTempCopyPath);

            var projectTool = new ProjectTool(projectTempCopyPath, new DebugLogger(true));
            return projectTool;
        }

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
                .Where(f => f.IncludeType != "PackageReference")
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

        private void Should_be_able_to_add_file_to_project(string projectRelativePath)
        {
            var projectTool = TempCopyAndGetProject(projectRelativePath);

            var scanFilesInProjectFolderBefore = projectTool.ScanFilesInProjectFolder();
            var compileFilesBefore = scanFilesInProjectFolderBefore
                .Where(f => f.IncludeType == "Compile")
                .Select(f => $"{f.IncludeType}:{f.Name}")
                .ToArray();

            var newFilePath = PathExtensions.GetAbsolutePath(projectTool.FolderPath, $"newCompileFile.cs");

            projectTool.AddFileToProject(newFilePath, "Compile");
            projectTool.Save();

            var newProjectTool = new ProjectTool(projectTool.FilePath, new ConsoleDebugLogger(true));
            var scanFilesInProjectFolderAfter = newProjectTool.ScanFilesInProjectFolder();
            var compileFilesAfter = scanFilesInProjectFolderAfter
                .Where(f => f.IncludeType == "Compile")
                .Select(f => $"{f.IncludeType}:{f.Name}")
                .OrderBy(o => o)
                .Debug(new ConsoleDebugLogger(true))
                .ToArray();

            compileFilesAfter.Should().BeEquivalentTo(
                // Admittedly this does nothing, as the file actually doesn't exist on disk for CPS projects. But the change to the project should be this (none)
                projectTool.IsCpsDocument ? compileFilesBefore : compileFilesBefore.Union(new string[] { "Compile:newCompileFile.cs" })
                    .OrderBy(o => o)
                    .Debug(new ConsoleDebugLogger(true))
                    .ToArray()
            );
        }

        private void Should_be_able_to_add_non_compile_non_content_file_to_project(string projectRelativePath)
        {
            var projectTool = TempCopyAndGetProject(projectRelativePath);

            var scanFilesInProjectFolderBefore = projectTool.ScanFilesInProjectFolder();
            var compileFilesBefore = scanFilesInProjectFolderBefore
                .Where(f => f.IncludeType == "Content")
                .Select(f => $"{f.IncludeType}:{f.Name}")
                .ToArray();

            var newFilePath = PathExtensions.GetAbsolutePath(projectTool.FolderPath, $"newContentFile.txt");

            projectTool.AddFileToProject(newFilePath, "Content");
            projectTool.Save();

            var newProjectTool = new ProjectTool(projectTool.FilePath, new ConsoleDebugLogger(true));
            var scanFilesInProjectFolderAfter = newProjectTool.ScanFilesInProjectFolder();
            var compileFilesAfter = scanFilesInProjectFolderAfter
                .Where(f => f.IncludeType == "Content")
                .Select(f => $"{f.IncludeType}:{f.Name}")
                .OrderBy(o => o)
                .Debug(new ConsoleDebugLogger(true))
                .ToArray();

            compileFilesAfter.Should().BeEquivalentTo(
                compileFilesBefore.Union(new string[] { "Content:newContentFile.txt" })
                    .OrderBy(o => o)
                    .Debug(new ConsoleDebugLogger(true))
                    .ToArray()
            );
        }

        private void Should_be_able_to_add_file_with_properties_to_project(string projectRelativePath)
        {
            var projectTool = TempCopyAndGetProject(projectRelativePath);

            var scanFilesInProjectFolderBefore = projectTool.ScanFilesInProjectFolder();
            var compileFilesBefore = scanFilesInProjectFolderBefore
                .Where(f => f.IncludeType == "Compile")
                .Select(f => $"{f.IncludeType}:{f.Name}:{f.Properties.Get("prop1")}:{f.Properties.Get("prop2")}")
                .ToArray();

            var newFilePath = PathExtensions.GetAbsolutePath(projectTool.FolderPath, $"newCompileFile.cs");
            var fileProperties = new Dictionary<string, string>() {{"prop1", "value1"}, {"prop2", "value2"}};

            projectTool.AddFileToProject(newFilePath, "Compile", fileProperties);
            projectTool.Save();

            var newProjectTool = new ProjectTool(projectTool.FilePath, new ConsoleDebugLogger(true));
            var scanFilesInProjectFolderAfter = newProjectTool.ScanFilesInProjectFolder();
            var compileFilesAfter = scanFilesInProjectFolderAfter
                .Where(f => f.IncludeType == "Compile")
                .Select(f => $"{f.IncludeType}:{f.Name}:{f.Properties.Get("prop1")}:{f.Properties.Get("prop2")}")
                .OrderBy(o => o)
                .Debug(new ConsoleDebugLogger(true))
                .ToArray();

            compileFilesAfter.Should().BeEquivalentTo(                
                compileFilesBefore.Union(new string[] { "Compile:newCompileFile.cs:value1:value2" })
                    .OrderBy(o => o)
                    .Debug(new ConsoleDebugLogger(true))
                    .ToArray()
            );
        }

        private void Should_be_able_to_add_excluded_file_with_properties_to_project(string projectRelativePath)
        {
            var projectTool = TempCopyAndGetProject(projectRelativePath);

            var scanFilesInProjectFolderBefore = projectTool.ScanFilesInProjectFolder();
            var compileFilesBefore = scanFilesInProjectFolderBefore
                .Where(f => f.IncludeType == "Compile")
                .Select(f => $"{f.IncludeType}:{f.Name}:{f.Properties.Get("prop1")}:{f.Properties.Get("prop2")}")
                .ToArray();

            var newFilePath = PathExtensions.GetAbsolutePath(projectTool.FolderPath, $"SampleClass3NotIncludedInproject.cs");
            var fileProperties = new Dictionary<string, string>() { { "prop1", "value1" }, { "prop2", "value2" } };

            projectTool.AddFileToProject(newFilePath, "Compile", fileProperties);
            projectTool.Save();

            var newProjectTool = new ProjectTool(projectTool.FilePath, new ConsoleDebugLogger(true));
            var scanFilesInProjectFolderAfter = newProjectTool.ScanFilesInProjectFolder();
            var compileFilesAfter = scanFilesInProjectFolderAfter
                .Where(f => f.IncludeType == "Compile")
                .Select(f => $"{f.IncludeType}:{f.Name}:{f.Properties.Get("prop1")}:{f.Properties.Get("prop2")}")
                .OrderBy(o => o)
                .Debug(new ConsoleDebugLogger(true))
                .ToArray();

            compileFilesAfter.Should().BeEquivalentTo(
                compileFilesBefore.Union(new string[] { "Compile:SampleClass3NotIncludedInproject.cs:value1:value2" })
                    .OrderBy(o => o)
                    .Debug(new ConsoleDebugLogger(true))
                    .ToArray()
            );
        }

        private void Should_be_able_to_remove_file_without_properties_from_project(string projectRelativePath)
        {
            var projectTool = TempCopyAndGetProject(projectRelativePath);

            var scanFilesInProjectFolderBefore = projectTool.ScanFilesInProjectFolder();
            var compileFilesBefore = scanFilesInProjectFolderBefore
                .Where(f => f.IncludeType == "Compile")
                .Select(f => $"{f.IncludeType}:{f.Name}:{f.Properties.Get("prop1")}:{f.Properties.Get("prop2")}")
                .ToArray();

            var removeFilePath = PathExtensions.GetAbsolutePath(projectTool.FolderPath, $"SampleClass1IncludedInproject.cs");

            projectTool.RemoveFileFromProject(removeFilePath);
            projectTool.Save();

            var newProjectTool = new ProjectTool(projectTool.FilePath, new ConsoleDebugLogger(true));
            var scanFilesInProjectFolderAfter = newProjectTool.ScanFilesInProjectFolder();
            var compileFilesAfter = scanFilesInProjectFolderAfter
                .Where(f => f.IncludeType == "Compile")
                .Select(f => $"{f.IncludeType}:{f.Name}:{f.Properties.Get("prop1")}:{f.Properties.Get("prop2")}")
                .OrderBy(o => o)
                .ToArray()
                .Debug(new ConsoleDebugLogger(true))
                ;

            compileFilesAfter.Should().BeEquivalentTo(
                compileFilesBefore.Except(new string[] { "Compile:SampleClass1IncludedInproject.cs::" })
                    .OrderBy(o => o)
                    .ToArray()
                    .Debug(new ConsoleDebugLogger(true))                    
            );
        }

        private void Should_be_able_to_determine_output_path_for_project(string projectRelativePath, string configuration, string platform, string expectedOutputPath)
        {
            var projectTool = TempCopyAndGetProject(projectRelativePath);

            var outputPath = projectTool.GetProjectOutputPath(configuration, platform);

            outputPath.Should().Be(expectedOutputPath);
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

        [Test]
        public void Should_be_able_to_add_file_to_classic_project()
        {
            Should_be_able_to_add_file_to_project(ClassicProjectRelativePath);
        }

        [Test]
        public void Should_be_able_to_add_file_to_cps_project()
        {
            Should_be_able_to_add_file_to_project(CPSProjectRelativePath);
        }


        [Test]
        public void Should_be_able_to_add_non_compile_non_content_file_to_classic_project()
        {
            Should_be_able_to_add_non_compile_non_content_file_to_project(ClassicProjectRelativePath);
        }

        [Test]
        public void Should_be_able_to_add_non_compile_non_content_file_to_cps_project()
        {
            Should_be_able_to_add_non_compile_non_content_file_to_project(CPSProjectRelativePath);
        }

        [Test]
        public void Should_be_able_to_add_file_with_properties_to_classic_project()
        {
            Should_be_able_to_add_file_with_properties_to_project(ClassicProjectRelativePath);
        }

        [Test]
        public void Should_be_able_to_add_file_with_properties_to_cps_project()
        {
            Should_be_able_to_add_file_with_properties_to_project(CPSProjectRelativePath);
        }

        [Test]
        public void Should_be_able_to_add_excluded_file_with_properties_to_classic_project()
        {
            Should_be_able_to_add_excluded_file_with_properties_to_project(ClassicProjectRelativePath);
        }

        [Test]
        public void Should_be_able_to_add_excluded_file_with_properties_to_cps_project()
        {
            Should_be_able_to_add_excluded_file_with_properties_to_project(CPSProjectRelativePath);
        }

        [Test]
        public void Should_be_able_to_remove_file_without_properties_from_classic_project()
        {
            Should_be_able_to_remove_file_without_properties_from_project(ClassicProjectRelativePath);
        }

        [Test]
        public void Should_be_able_to_remove_file_without_properties_from_cps_project()
        {
            Should_be_able_to_remove_file_without_properties_from_project(CPSProjectRelativePath);
        }

        [Test]
        public void Should_be_able_to_determine_output_path_for_classic_project()
        {
            Should_be_able_to_determine_output_path_for_project(ClassicProjectRelativePath, "Debug", "AnyCPU", @"bin\Debug\special_bin\");
            Should_be_able_to_determine_output_path_for_project(ClassicProjectRelativePath, "Debug", "x64", @"bin\x64\Debug");
            Should_be_able_to_determine_output_path_for_project(ClassicProjectRelativePath, "Release", "AnyCPU", @"bin\Release\special_releaseBin");
            Should_be_able_to_determine_output_path_for_project(ClassicProjectRelativePath, "Release", "x64", @"bin\x64\Release");
        }

        [Test]
        public void Should_be_able_to_determine_output_path_for_CPS_project()
        {
            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePath, "Debug", "AnyCPU", @"bin\Debug\net461\win7-x64\special_bin\net461\win7-x64");
            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePath, "Release", "AnyCPU", @"bin\Release\net461\win7-x64");

            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPath, "Debug", "AnyCPU", @"bin\Debug\net461");
            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPath, "Release", "AnyCPU", @"bin\Release\net461");
            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPath, "Debug", "x64", @"bin\x64\Debug\net461");
            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPath, "Release", "x64", @"bin\x64\Release\net461");

            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPathNoRuntime, "Debug", "AnyCPU", @"bin\Debug\net461");
            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPathNoRuntime, "Release", "AnyCPU", @"bin\Release\net461");
            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPathNoRuntime, "Debug", "x64", @"bin\x64\Debug\net461");
            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPathNoRuntime, "Release", "x64", @"bin\x64\Release\net461");

            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPathx64, "Debug", "AnyCPU", @"bin\Debug\net461\win7-x64");
            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPathx64, "Release", "AnyCPU", @"bin\Release\net461\win7-x64");
            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPathx64, "Debug", "x64", @"bin\x64\Debug\net461\win7-x64");
            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPathx64, "Release", "x64", @"bin\x64\Release\net461\win7-x64");

            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPathx64NoRuntime, "Debug", "AnyCPU", @"bin\Debug\net461");
            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPathx64NoRuntime, "Release", "AnyCPU", @"bin\Release\net461");
            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPathx64NoRuntime, "Debug", "x64", @"bin\x64\Debug\net461");
            Should_be_able_to_determine_output_path_for_project(CPSProjectRelativePathNoOutputPathx64NoRuntime, "Release", "x64", @"bin\x64\Release\net461");
        }
    }
}