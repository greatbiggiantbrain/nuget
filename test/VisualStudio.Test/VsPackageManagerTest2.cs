﻿using System;
using System.Linq;
using System.Runtime.Versioning;
using EnvDTE;
using Moq;
using NuGet.Test;
using NuGet.Test.Mocks;
using Xunit;
using Xunit.Extensions;

namespace NuGet.VisualStudio.Test
{
    using PackageUtility = NuGet.Test.PackageUtility;
    using System.Collections.Generic;

    public partial class VsPackageManagerTest
    {
        [Fact]
        public void ReinstallPackagesRestoresPackageWithTheSameVersion()
        {
            // Arrange
            var localRepository = new Mock<MockPackageRepository>() { CallBase = true }.As<ISharedPackageRepository>().Object;
            var sourceRepository = new MockPackageRepository();
            var projectSystem = new MockProjectSystem();
            var pathResolver = new DefaultPackagePathResolver(projectSystem);
            var projectManager = new ProjectManager(localRepository, pathResolver, projectSystem, new MockPackageRepository());

            var packageManager = new VsPackageManager(
                TestUtils.GetSolutionManager(),
                sourceRepository,
                new Mock<IFileSystemProvider>().Object,
                projectSystem,
                localRepository,
                new Mock<VsPackageInstallerEvents>().Object);

            var packageA = PackageUtility.CreatePackage("A", "1.2", new[] { "content.txt" });
            sourceRepository.Add(packageA);
            localRepository.AddPackage(packageA);
            projectManager.LocalRepository.AddPackage(packageA);

            // Act
            packageManager.ReinstallPackage(projectManager, "A", updateDependencies: true, allowPrereleaseVersions: true, logger: null);

            // Assert
            Assert.True(packageManager.LocalRepository.Exists("A", new SemanticVersion("1.2")));
            Assert.True(projectManager.LocalRepository.Exists("A", new SemanticVersion("1.2")));
        }

        [Fact]
        public void ReinstallPackagesRestoresPackageWithTheSamePrereleaseVersion()
        {
            // Arrange
            var localRepository = new Mock<MockPackageRepository>() { CallBase = true }.As<ISharedPackageRepository>().Object;
            var sourceRepository = new MockPackageRepository();
            var projectSystem = new MockProjectSystem();
            var pathResolver = new DefaultPackagePathResolver(projectSystem);
            var projectManager = new ProjectManager(localRepository, pathResolver, projectSystem, new MockPackageRepository());

            var packageManager = new VsPackageManager(
                TestUtils.GetSolutionManager(),
                sourceRepository,
                new Mock<IFileSystemProvider>().Object,
                projectSystem,
                localRepository,
                new Mock<VsPackageInstallerEvents>().Object);

            var packageA = PackageUtility.CreatePackage("A", "1.2-alpha", new[] { "content.txt" });
            sourceRepository.Add(packageA);
            localRepository.AddPackage(packageA);
            projectManager.LocalRepository.AddPackage(packageA);

            // Act
            packageManager.ReinstallPackage(projectManager, "A", updateDependencies: true, allowPrereleaseVersions: false, logger: null);

            // Assert
            Assert.True(packageManager.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));
            Assert.True(projectManager.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));
        }

        [Fact]
        public void ReinstallPackagesRestoresPackagesWithDependencies()
        {
            // Arrange
            var localRepository = new Mock<MockPackageRepository>() { CallBase = true }.As<ISharedPackageRepository>().Object;
            var sourceRepository = new MockPackageRepository();
            var projectSystem = new MockProjectSystem();
            var pathResolver = new DefaultPackagePathResolver(projectSystem);
            var projectManager = new ProjectManager(localRepository, pathResolver, projectSystem, new MockPackageRepository());

            var packageManager = new VsPackageManager(
                TestUtils.GetSolutionManager(),
                sourceRepository,
                new Mock<IFileSystemProvider>().Object,
                projectSystem,
                localRepository,
                new Mock<VsPackageInstallerEvents>().Object);

            var packageA = PackageUtility.CreatePackage("A", "1.2-alpha", new[] { "content.txt" }, dependencies: new[] { new PackageDependency("B") });
            var packageB = PackageUtility.CreatePackage("B", "2.0.0", new[] { "hello.txt" });

            sourceRepository.Add(packageA);
            localRepository.AddPackage(packageA);
            projectManager.LocalRepository.AddPackage(packageA);

            sourceRepository.Add(packageB);
            localRepository.AddPackage(packageB);
            projectManager.LocalRepository.AddPackage(packageB);

            // Act
            packageManager.ReinstallPackage(projectManager, "A", updateDependencies: true, allowPrereleaseVersions: false, logger: null);

            // Assert
            Assert.True(packageManager.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));
            Assert.True(projectManager.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));

            Assert.True(packageManager.LocalRepository.Exists("B", new SemanticVersion("2.0.0")));
            Assert.True(projectManager.LocalRepository.Exists("B", new SemanticVersion("2.0.0")));
        }

        [Fact]
        public void ReinstallPackagesRestoresPackagesWithNewContentIfProjectFrameworkChanges()
        {
            // Arrange
            var localRepository = new Mock<MockPackageRepository>() { CallBase = true }.As<ISharedPackageRepository>().Object;
            var sourceRepository = new MockPackageRepository();
            var projectSystem = new MockProjectSystem(new FrameworkName(".NETFramework, Version=v3.0"));
            var pathResolver = new DefaultPackagePathResolver(projectSystem);

            var packageReferenceRepository = new PackageReferenceRepository(projectSystem, localRepository);
            var projectManager = new ProjectManager(localRepository, pathResolver, projectSystem, packageReferenceRepository);

            var packageManager = new VsPackageManager(
                TestUtils.GetSolutionManager(),
                sourceRepository,
                new Mock<IFileSystemProvider>().Object,
                projectSystem,
                localRepository,
                new Mock<VsPackageInstallerEvents>().Object);

            var packageA = PackageUtility.CreatePackage(
                "A",
                "1.2-alpha",
                new[] { "net30\\content.txt", "silverlight40\\content4.txt" },
                new[] { "lib\\net30\\ref.dll", "lib\\silverlight40\\refsl.dll" });

            sourceRepository.Add(packageA);

            packageManager.InstallPackage(projectManager, "A", new SemanticVersion("1.2-alpha"), ignoreDependencies: false, allowPrereleaseVersions: true, logger: null);
            Assert.True(packageManager.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));
            Assert.True(projectManager.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));
            Assert.True(projectSystem.FileExists("content.txt"));
            Assert.False(projectSystem.FileExists("content4.txt"));
            Assert.True(projectSystem.ReferenceExists("ref.dll"));
            Assert.False(projectSystem.ReferenceExists("refsl.dll"));

            // now change project's target framework to silverilght
            projectSystem.ChangeTargetFramework(new FrameworkName("Silverlight, Version=v4.0"));

            // Act
            packageManager.ReinstallPackage(projectManager, "A", updateDependencies: true, allowPrereleaseVersions: false, logger: null);

            // Assert
            Assert.True(packageManager.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));
            Assert.True(projectManager.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));
            Assert.True(projectSystem.FileExists("content4.txt"));
            Assert.False(projectSystem.FileExists("content.txt"));
            Assert.False(projectSystem.ReferenceExists("ref.dll"));
            Assert.True(projectSystem.ReferenceExists("refsl.dll"));
        }

        [Fact]
        public void ReinstallPackagesRestoresPackagesWithNewDependencyIfProjectFrameworkChanges()
        {
            // Arrange
            var localRepository = new Mock<MockPackageRepository>() { CallBase = true }.As<ISharedPackageRepository>().Object;
            var sourceRepository = new MockPackageRepository();
            var projectSystem = new MockProjectSystem(new FrameworkName(".NETFramework, Version=v4.0"));
            var pathResolver = new DefaultPackagePathResolver(projectSystem);

            var packageReferenceRepository = new PackageReferenceRepository(projectSystem, localRepository);
            var projectManager = new ProjectManager(localRepository, pathResolver, projectSystem, packageReferenceRepository);

            var packageManager = new VsPackageManager(
                TestUtils.GetSolutionManager(),
                sourceRepository,
                new Mock<IFileSystemProvider>().Object,
                projectSystem,
                localRepository,
                new Mock<VsPackageInstallerEvents>().Object);

            var packageA = PackageUtility.CreatePackageWithDependencySets(
                "A",
                "1.2-alpha",
                new[] { "contentA.txt" },
                dependencySets: new PackageDependencySet[] {
                    new PackageDependencySet(new FrameworkName(".NETFramework, Version=v4.0"),
                                             new [] { new PackageDependency("B")}),
                    new PackageDependencySet(new FrameworkName("Silverlight, Version=v5.0"),
                                             new [] { new PackageDependency("C")})
                });

            var packageB = PackageUtility.CreatePackage(
                "B",
                "1.0",
                new[] { "contentB.txt" });

            var packageC = PackageUtility.CreatePackage(
                "C",
                "2.0-beta",
                new[] { "contentC.txt" });

            sourceRepository.Add(packageA);
            sourceRepository.Add(packageB);
            sourceRepository.Add(packageC);

            packageManager.InstallPackage(projectManager, "A", new SemanticVersion("1.2-alpha"), ignoreDependencies: false, allowPrereleaseVersions: true, logger: null);
            Assert.True(packageManager.LocalRepository.Exists("A"));
            Assert.True(projectManager.LocalRepository.Exists("A"));
            Assert.True(packageManager.LocalRepository.Exists("B"));
            Assert.True(projectManager.LocalRepository.Exists("B"));
            Assert.False(packageManager.LocalRepository.Exists("C"));
            Assert.False(projectManager.LocalRepository.Exists("C"));
            Assert.True(projectSystem.FileExists("contentA.txt"));
            Assert.True(projectSystem.FileExists("contentB.txt"));
            Assert.False(projectSystem.FileExists("contentC.txt"));

            // now change project's target framework to silverilght
            projectSystem.ChangeTargetFramework(new FrameworkName("Silverlight, Version=v5.0"));

            // Act
            packageManager.ReinstallPackage(projectManager, "A", updateDependencies: true, allowPrereleaseVersions: true, logger: null);

            // Assert
            Assert.True(packageManager.LocalRepository.Exists("A"));
            Assert.True(projectManager.LocalRepository.Exists("A"));
            Assert.False(packageManager.LocalRepository.Exists("B"));
            Assert.False(projectManager.LocalRepository.Exists("B"));
            Assert.True(packageManager.LocalRepository.Exists("C"));
            Assert.True(projectManager.LocalRepository.Exists("C"));
            Assert.True(projectSystem.FileExists("contentA.txt"));
            Assert.False(projectSystem.FileExists("contentB.txt"));
            Assert.True(projectSystem.FileExists("contentC.txt"));
        }

        [Fact]
        public void ReinstallPackagesDoesNotRestorePackagesWithNewDependencyWhenProjectFrameworkChangesIfUpdateDependenciesIsFalse()
        {
            // Arrange
            var localRepository = new Mock<MockPackageRepository>() { CallBase = true }.As<ISharedPackageRepository>().Object;
            var sourceRepository = new MockPackageRepository();
            var projectSystem = new MockProjectSystem(new FrameworkName(".NETFramework, Version=v4.0"));
            var pathResolver = new DefaultPackagePathResolver(projectSystem);

            var packageReferenceRepository = new PackageReferenceRepository(projectSystem, localRepository);
            var projectManager = new ProjectManager(localRepository, pathResolver, projectSystem, packageReferenceRepository);

            var packageManager = new VsPackageManager(
                TestUtils.GetSolutionManager(),
                sourceRepository,
                new Mock<IFileSystemProvider>().Object,
                projectSystem,
                localRepository,
                new Mock<VsPackageInstallerEvents>().Object);

            var packageA = PackageUtility.CreatePackageWithDependencySets(
                "A",
                "1.2-alpha",
                new[] { "contentA.txt" },
                dependencySets: new PackageDependencySet[] {
                    new PackageDependencySet(new FrameworkName(".NETFramework, Version=v4.0"),
                                             new [] { new PackageDependency("B")}),
                    new PackageDependencySet(new FrameworkName("Silverlight, Version=v5.0"),
                                             new [] { new PackageDependency("C")})
                });

            var packageB = PackageUtility.CreatePackage(
                "B",
                "1.0",
                new[] { "contentB.txt" });

            var packageC = PackageUtility.CreatePackage(
                "C",
                "2.0-beta",
                new[] { "contentC.txt" });

            sourceRepository.Add(packageA);
            sourceRepository.Add(packageB);
            sourceRepository.Add(packageC);

            packageManager.InstallPackage(projectManager, "A", new SemanticVersion("1.2-alpha"), ignoreDependencies: false, allowPrereleaseVersions: true, logger: null);
            Assert.True(packageManager.LocalRepository.Exists("A"));
            Assert.True(projectManager.LocalRepository.Exists("A"));
            Assert.True(packageManager.LocalRepository.Exists("B"));
            Assert.True(projectManager.LocalRepository.Exists("B"));
            Assert.False(packageManager.LocalRepository.Exists("C"));
            Assert.False(projectManager.LocalRepository.Exists("C"));
            Assert.True(projectSystem.FileExists("contentA.txt"));
            Assert.True(projectSystem.FileExists("contentB.txt"));
            Assert.False(projectSystem.FileExists("contentC.txt"));

            // now change project's target framework to silverilght
            projectSystem.ChangeTargetFramework(new FrameworkName("Silverlight, Version=v5.0"));

            // Act
            packageManager.ReinstallPackage(projectManager, "A", updateDependencies: false, allowPrereleaseVersions: true, logger: null);

            // Assert
            Assert.True(packageManager.LocalRepository.Exists("A"));
            Assert.True(projectManager.LocalRepository.Exists("A"));
            Assert.True(packageManager.LocalRepository.Exists("B"));
            Assert.True(projectManager.LocalRepository.Exists("B"));
            Assert.False(packageManager.LocalRepository.Exists("C"));
            Assert.False(projectManager.LocalRepository.Exists("C"));
            Assert.True(projectSystem.FileExists("contentA.txt"));
            Assert.True(projectSystem.FileExists("contentB.txt"));
            Assert.False(projectSystem.FileExists("contentC.txt"));
        }

        [Fact]
        public void ReinstallPackagesThrowsWithNewDependencyWhenProjectFrameworkChangesIfAllowPrereleaseParameterIsFalse()
        {
            // Arrange
            var localRepository = new Mock<MockPackageRepository>() { CallBase = true }.As<ISharedPackageRepository>().Object;
            var sourceRepository = new MockPackageRepository();
            var projectSystem = new MockProjectSystem(new FrameworkName(".NETFramework, Version=v4.0"));
            var pathResolver = new DefaultPackagePathResolver(projectSystem);

            var packageReferenceRepository = new PackageReferenceRepository(projectSystem, localRepository);
            var projectManager = new ProjectManager(localRepository, pathResolver, projectSystem, packageReferenceRepository);

            var packageManager = new VsPackageManager(
                TestUtils.GetSolutionManager(),
                sourceRepository,
                new Mock<IFileSystemProvider>().Object,
                projectSystem,
                localRepository,
                new Mock<VsPackageInstallerEvents>().Object);

            var packageA = PackageUtility.CreatePackageWithDependencySets(
                "A",
                "1.2-alpha",
                new[] { "contentA.txt" },
                dependencySets: new PackageDependencySet[] {
                    new PackageDependencySet(new FrameworkName(".NETFramework, Version=v4.0"),
                                             new [] { new PackageDependency("B")}),
                    new PackageDependencySet(new FrameworkName("Silverlight, Version=v5.0"),
                                             new [] { new PackageDependency("C")})
                });

            var packageB = PackageUtility.CreatePackage(
                "B",
                "1.0",
                new[] { "contentB.txt" });

            var packageC = PackageUtility.CreatePackage(
                "C",
                "2.0-beta",
                new[] { "contentC.txt" });

            sourceRepository.Add(packageA);
            sourceRepository.Add(packageB);
            sourceRepository.Add(packageC);

            packageManager.InstallPackage(projectManager, "A", new SemanticVersion("1.2-alpha"), ignoreDependencies: false, allowPrereleaseVersions: true, logger: null);
            Assert.True(packageManager.LocalRepository.Exists("A"));
            Assert.True(projectManager.LocalRepository.Exists("A"));
            Assert.True(packageManager.LocalRepository.Exists("B"));
            Assert.True(projectManager.LocalRepository.Exists("B"));
            Assert.False(packageManager.LocalRepository.Exists("C"));
            Assert.False(projectManager.LocalRepository.Exists("C"));
            Assert.True(projectSystem.FileExists("contentA.txt"));
            Assert.True(projectSystem.FileExists("contentB.txt"));
            Assert.False(projectSystem.FileExists("contentC.txt"));

            // now change project's target framework to silverilght
            projectSystem.ChangeTargetFramework(new FrameworkName("Silverlight, Version=v5.0"));

            // Act && Assert

            ExceptionAssert.Throws<InvalidOperationException>(
                () => packageManager.ReinstallPackage(projectManager, "A", updateDependencies: true, allowPrereleaseVersions: false, logger: null),
                "Unable to resolve dependency 'C'."
            );
        }

        [Fact]
        public void ReinstallPackagesRestoresAllPackagesInAProjectWithNewContentIfProjectFrameworkChanges()
        {
            // Arrange
            var localRepository = new Mock<MockPackageRepository>() { CallBase = true }.As<ISharedPackageRepository>().Object;
            var sourceRepository = new MockPackageRepository();
            var projectSystem = new MockProjectSystem(new FrameworkName(".NETFramework, Version=v3.0"));
            var pathResolver = new DefaultPackagePathResolver(projectSystem);

            var packageReferenceRepository = new PackageReferenceRepository(projectSystem, localRepository);
            var projectManager = new ProjectManager(localRepository, pathResolver, projectSystem, packageReferenceRepository);

            var packageManager = new VsPackageManager(
                TestUtils.GetSolutionManager(),
                sourceRepository,
                new Mock<IFileSystemProvider>().Object,
                projectSystem,
                localRepository,
                new Mock<VsPackageInstallerEvents>().Object);

            var packageA = PackageUtility.CreatePackage(
                "A",
                "1.2-alpha",
                new[] { "net30\\content.txt", "silverlight40\\content4.txt" },
                new[] { "lib\\net30\\ref.dll", "lib\\silverlight40\\refsl.dll" });

            var packageB = PackageUtility.CreatePackage(
                "B",
                "2.0",
                new[] { "net30\\contentB.txt", "silverlight40\\content4B.txt" },
                new[] { "lib\\net30\\refB.dll", "lib\\silverlight40\\refslB.dll" });

            sourceRepository.Add(packageA);
            sourceRepository.Add(packageB);

            packageManager.InstallPackage(projectManager, "A", new SemanticVersion("1.2-alpha"), ignoreDependencies: false, allowPrereleaseVersions: true, logger: null);
            packageManager.InstallPackage(projectManager, "B", new SemanticVersion("2.0"), ignoreDependencies: false, allowPrereleaseVersions: true, logger: null);

            Assert.True(packageManager.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));
            Assert.True(projectManager.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));
            Assert.True(projectSystem.FileExists("content.txt"));
            Assert.False(projectSystem.FileExists("content4.txt"));
            Assert.True(projectSystem.ReferenceExists("ref.dll"));
            Assert.False(projectSystem.ReferenceExists("refsl.dll"));

            Assert.True(packageManager.LocalRepository.Exists("B"));
            Assert.True(projectManager.LocalRepository.Exists("B"));
            Assert.True(projectSystem.FileExists("contentB.txt"));
            Assert.False(projectSystem.FileExists("content4B.txt"));
            Assert.True(projectSystem.ReferenceExists("refB.dll"));
            Assert.False(projectSystem.ReferenceExists("refslB.dll"));

            // now change project's target framework to silverlight
            projectSystem.ChangeTargetFramework(new FrameworkName("Silverlight, Version=v4.0"));

            // Act
            packageManager.ReinstallPackages(projectManager, updateDependencies: true, allowPrereleaseVersions: false, logger: null);

            // Assert
            Assert.True(packageManager.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));
            Assert.True(projectManager.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));
            Assert.False(projectSystem.FileExists("content.txt"));
            Assert.True(projectSystem.FileExists("content4.txt"));
            Assert.False(projectSystem.ReferenceExists("ref.dll"));
            Assert.True(projectSystem.ReferenceExists("refsl.dll"));

            Assert.True(packageManager.LocalRepository.Exists("B"));
            Assert.True(projectManager.LocalRepository.Exists("B"));
            Assert.False(projectSystem.FileExists("contentB.txt"));
            Assert.True(projectSystem.FileExists("content4B.txt"));
            Assert.False(projectSystem.ReferenceExists("refB.dll"));
            Assert.True(projectSystem.ReferenceExists("refslB.dll"));
        }

        [Fact]
        public void ReinstallPackagesRestoresPackageInAllProjectsWithNewContentIfProjectFrameworkChanges()
        {
            // Arrange
            var localRepository = new Mock<MockPackageRepository>() { CallBase = true }.As<ISharedPackageRepository>().Object;
            var sourceRepository = new MockPackageRepository();

            var projectSystem1 = new MockProjectSystem(new FrameworkName(".NETFramework, Version=v3.0"));
            var pathResolver1 = new DefaultPackagePathResolver(projectSystem1);
            var packageReferenceRepository1 = new PackageReferenceRepository(projectSystem1, localRepository);
            var projectManager1 = new ProjectManager(localRepository, pathResolver1, projectSystem1, packageReferenceRepository1);

            var projectSystem2 = new MockProjectSystem(new FrameworkName(".NETCore, Version=v4.5"));
            var pathResolver2 = new DefaultPackagePathResolver(projectSystem2);
            var packageReferenceRepository2 = new PackageReferenceRepository(projectSystem2, localRepository);
            var projectManager2 = new ProjectManager(localRepository, pathResolver2, projectSystem2, packageReferenceRepository2);

            var project1 = TestUtils.GetProject("Project1");
            var project2 = TestUtils.GetProject("Project2");

            var packageManager = new MockVsPackageManager(
                TestUtils.GetSolutionManager(projects: new[] { project1, project2 }),
                sourceRepository,
                new Mock<IFileSystemProvider>().Object,
                projectSystem2,
                localRepository,
                new Mock<VsPackageInstallerEvents>().Object);

            packageManager.RegisterProjectManager(project1, projectManager1);
            packageManager.RegisterProjectManager(project2, projectManager2);

            var packageA = PackageUtility.CreatePackage(
                "A",
                "1.2-alpha",
                new[] { "net30\\content.txt", "silverlight40\\content4.txt" },
                new[] { "lib\\net30\\ref.dll", "lib\\silverlight40\\refsl.dll" });

            var packageB = PackageUtility.CreatePackage(
                "B",
                "2.0",
                new[] { "winrt45\\hello.txt", "sl4-wp71\\world.txt" },
                new[] { "lib\\winrt45\\comma.dll", "lib\\sl4-wp71\\dude.dll" });

            sourceRepository.Add(packageA);
            sourceRepository.Add(packageB);

            // install package A -> project 1
            // and package B -> project 2
            packageManager.InstallPackage(projectManager1, "A", new SemanticVersion("1.2-alpha"), ignoreDependencies: false, allowPrereleaseVersions: true, logger: null);
            packageManager.InstallPackage(projectManager2, "B", new SemanticVersion("2.0"), ignoreDependencies: false, allowPrereleaseVersions: true, logger: null);

            Assert.True(packageManager.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));
            Assert.True(projectManager1.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));
            Assert.True(projectSystem1.FileExists("content.txt"));
            Assert.False(projectSystem1.FileExists("content4.txt"));
            Assert.True(projectSystem1.ReferenceExists("ref.dll"));
            Assert.False(projectSystem1.ReferenceExists("refsl.dll"));

            Assert.True(packageManager.LocalRepository.Exists("B", new SemanticVersion("2.0")));
            Assert.True(projectManager2.LocalRepository.Exists("B", new SemanticVersion("2.0")));
            Assert.True(projectSystem2.FileExists("hello.txt"));
            Assert.False(projectSystem2.FileExists("world.txt"));
            Assert.True(projectSystem2.ReferenceExists("comma.dll"));
            Assert.False(projectSystem2.ReferenceExists("dude.dll"));

            // now change project's target framework to silverlight
            projectSystem1.ChangeTargetFramework(new FrameworkName("Silverlight, Version=v4.0"));
            projectSystem2.ChangeTargetFramework(new FrameworkName("Silverlight, Version=v4.0, Profile=WindowsPhone71"));

            // Act
            packageManager.ReinstallPackages(updateDependencies: true, allowPrereleaseVersions: false, logger: null, eventListener: NullPackageOperationEventListener.Instance);

            // Assert
            Assert.True(packageManager.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));
            Assert.True(projectManager1.LocalRepository.Exists("A", new SemanticVersion("1.2-alpha")));

            Assert.False(projectSystem1.FileExists("content.txt"));
            Assert.True(projectSystem1.FileExists("content4.txt"));
            Assert.False(projectSystem1.ReferenceExists("ref.dll"));
            Assert.True(projectSystem1.ReferenceExists("refsl.dll"));

            Assert.True(packageManager.LocalRepository.Exists("B", new SemanticVersion("2.0")));
            Assert.True(projectManager2.LocalRepository.Exists("B", new SemanticVersion("2.0")));

            Assert.False(projectSystem2.FileExists("hello.txt"));
            Assert.True(projectSystem2.FileExists("world.txt"));
            Assert.False(projectSystem2.ReferenceExists("comma.dll"));
            Assert.True(projectSystem2.ReferenceExists("dude.dll"));
        }

        private class MockVsPackageManager : VsPackageManager
        {
            private Dictionary<string, IProjectManager> _projectToManagers = 
                new Dictionary<string, IProjectManager>();

            public MockVsPackageManager(
                ISolutionManager solutionManager,
                IPackageRepository sourceRepository,
                IFileSystemProvider fileSystemProvider,
                IFileSystem fileSystem,
                ISharedPackageRepository sharedRepository,
                VsPackageInstallerEvents packageEvents) :
                base(
                    solutionManager,
                    sourceRepository,
                    fileSystemProvider,
                    fileSystem,
                    sharedRepository,
                    packageEvents)
            {
            }

            public void RegisterProjectManager(Project project, IProjectManager projectManager)
            {
                _projectToManagers[project.Name] = projectManager;
            }

            public override IProjectManager GetProjectManager(Project project)
            {
                return _projectToManagers[project.Name];
            }
        }
    }
}