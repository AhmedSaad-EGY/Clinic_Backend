using System.Xml.Linq;

namespace Clinic.ArchitectureTests;

public sealed class ProjectDependencyTests
{
    public static TheoryData<string, string[]> AllowedProjectReferences => new()
    {
        { "Clinic.Domain", [] },
        { "Clinic.Application", ["Clinic.Domain"] },
        { "Clinic.Infrastructure", ["Clinic.Application", "Clinic.Domain"] },
        { "Clinic.Api", ["Clinic.Application", "Clinic.Infrastructure"] },
    };

    [Theory]
    [MemberData(nameof(AllowedProjectReferences))]
    public void ProductionProjectHasOnlyAllowedDependencies(
        string projectName,
        string[] expectedReferences)
    {
        string projectPath = Path.Combine(
            FindBackendRoot(),
            "src",
            projectName,
            $"{projectName}.csproj");

        XDocument project = XDocument.Load(projectPath);
        string projectDirectory = Path.GetDirectoryName(projectPath)!;

        string[] actualReferences = project
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(
                Path.GetFullPath(Path.Combine(projectDirectory, include!))))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedReferences.Order(StringComparer.Ordinal), actualReferences);
    }

    private static string FindBackendRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Clinic.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the backend solution root.");
    }
}
