using System.Reflection;

namespace TrayApp.Services
{
    internal static class AppPaths
    {
        private const string ProjectFileName = "TrayApp.csproj";

        private static readonly Lazy<string> SourceRootLoader = new(ResolveSourceRoot);

        public static string SourceRoot => SourceRootLoader.Value;

        public static string GetSourceRootFilePath(string fileName)
        {
            string? sourceRootPath = TryGetSourceRootFilePath(fileName);

            if (sourceRootPath != null && File.Exists(sourceRootPath))
            {
                return sourceRootPath;
            }

            string currentDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            if (File.Exists(currentDirectoryPath))
            {
                return currentDirectoryPath;
            }

            string appBaseDirectoryPath = Path.Combine(AppContext.BaseDirectory, fileName);
            if (File.Exists(appBaseDirectoryPath))
            {
                return appBaseDirectoryPath;
            }

            if (sourceRootPath != null)
            {
                return sourceRootPath;
            }

            return currentDirectoryPath;
        }

        private static string? TryGetSourceRootFilePath(string fileName)
        {
            try
            {
                return Path.Combine(SourceRoot, fileName);
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }

        private static string ResolveSourceRoot()
        {
            string? startDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (string.IsNullOrWhiteSpace(startDirectory))
            {
                throw new InvalidOperationException("Unable to resolve the application directory.");
            }

            DirectoryInfo? current = new DirectoryInfo(startDirectory);

            while (current != null)
            {
                string projectFilePath = Path.Combine(current.FullName, ProjectFileName);

                if (File.Exists(projectFilePath))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException(
                $"Could not locate the project source root containing '{ProjectFileName}' starting from '{startDirectory}'.");
        }
    }
}