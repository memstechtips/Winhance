using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Winhance.Core.Interfaces.Services;

namespace Winhance.Infrastructure.FileSystem
{
    /// <summary>
    /// Implementation of IFileSystem that wraps the System.IO operations.
    /// </summary>
    public class FileSystemService : IFileSystemService
    {
        /// <summary>
        /// Determines whether the specified file exists.
        /// </summary>
        /// <param name="path">The file to check.</param>
        /// <returns>True if the file exists; otherwise, false.</returns>
        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        /// <summary>
        /// Determines whether the specified directory exists.
        /// </summary>
        /// <param name="path">The directory to check.</param>
        /// <returns>True if the directory exists; otherwise, false.</returns>
        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        /// <summary>
        /// Creates a directory if it doesn't exist.
        /// </summary>
        /// <param name="path">The directory to create.</param>
        /// <returns>True if the directory was created or already exists; otherwise, false.</returns>
        public bool CreateDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reads all text from a file.
        /// </summary>
        /// <param name="path">The file to read from.</param>
        /// <returns>The text read from the file.</returns>
        public string ReadAllText(string path)
        {
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Reads all text from a file asynchronously.
        /// </summary>
        /// <param name="path">The file to read from.</param>
        /// <returns>A task that represents the asynchronous read operation and wraps the text read from the file.</returns>
        public async Task<string> ReadAllTextAsync(string path)
        {
            using var reader = new StreamReader(path);
            return await reader.ReadToEndAsync();
        }

        /// <summary>
        /// Reads all bytes from a file.
        /// </summary>
        /// <param name="path">The file to read from.</param>
        /// <returns>The bytes read from the file.</returns>
        public byte[] ReadAllBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        /// <summary>
        /// Reads all bytes from a file asynchronously.
        /// </summary>
        /// <param name="path">The file to read from.</param>
        /// <returns>A task that represents the asynchronous read operation and wraps the bytes read from the file.</returns>
        public async Task<byte[]> ReadAllBytesAsync(string path)
        {
            return await File.ReadAllBytesAsync(path);
        }

        /// <summary>
        /// Writes text to a file.
        /// </summary>
        /// <param name="path">The file to write to.</param>
        /// <param name="contents">The text to write to the file.</param>
        /// <returns>True if the text was written successfully; otherwise, false.</returns>
        public bool WriteAllText(string path, string contents)
        {
            try
            {
                // Ensure the directory exists
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, contents);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Writes text to a file asynchronously.
        /// </summary>
        /// <param name="path">The file to write to.</param>
        /// <param name="contents">The text to write to the file.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        public async Task WriteAllTextAsync(string path, string contents)
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(path, contents);
        }

        /// <summary>
        /// Appends text to a file.
        /// </summary>
        /// <param name="path">The file to append to.</param>
        /// <param name="contents">The text to append to the file.</param>
        public void AppendAllText(string path, string contents)
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(path, contents);
        }

        /// <summary>
        /// Appends text to a file asynchronously.
        /// </summary>
        /// <param name="path">The file to append to.</param>
        /// <param name="contents">The text to append to the file.</param>
        /// <returns>A task that represents the asynchronous append operation.</returns>
        public async Task AppendAllTextAsync(string path, string contents)
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.AppendAllTextAsync(path, contents);
        }

        /// <summary>
        /// Writes bytes to a file.
        /// </summary>
        /// <param name="path">The file to write to.</param>
        /// <param name="bytes">The bytes to write to the file.</param>
        public void WriteAllBytes(string path, byte[] bytes)
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, bytes);
        }

        /// <summary>
        /// Writes bytes to a file asynchronously.
        /// </summary>
        /// <param name="path">The file to write to.</param>
        /// <param name="bytes">The bytes to write to the file.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        public async Task WriteAllBytesAsync(string path, byte[] bytes)
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(path, bytes);
        }

        /// <summary>
        /// Deletes a file.
        /// </summary>
        /// <param name="path">The file to delete.</param>
        /// <returns>True if the file was deleted or didn't exist; otherwise, false.</returns>
        public bool DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Deletes a directory.
        /// </summary>
        /// <param name="path">The directory to delete.</param>
        /// <param name="recursive">True to delete subdirectories and files; otherwise, false.</param>
        public void DeleteDirectory(string path, bool recursive)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive);
            }
        }

        /// <summary>
        /// Gets all files in a directory that match the specified pattern.
        /// </summary>
        /// <param name="path">The directory to search.</param>
        /// <param name="pattern">The search pattern.</param>
        /// <param name="recursive">Whether to search subdirectories.</param>
        /// <returns>A collection of file paths.</returns>
        public IEnumerable<string> GetFiles(string path, string pattern, bool recursive)
        {
            return Directory.GetFiles(path, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// Gets all directories in a directory that match the specified pattern.
        /// </summary>
        /// <param name="path">The directory to search.</param>
        /// <param name="pattern">The search pattern.</param>
        /// <param name="recursive">Whether to search subdirectories.</param>
        /// <returns>A collection of directory paths.</returns>
        public IEnumerable<string> GetDirectories(string path, string pattern, bool recursive)
        {
            return Directory.GetDirectories(path, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// Gets the path to a special folder, such as AppData, ProgramFiles, etc.
        /// </summary>
        /// <param name="specialFolder">The name of the special folder.</param>
        /// <returns>The path to the special folder.</returns>
        public string GetSpecialFolderPath(string specialFolder)
        {
            if (Enum.TryParse<Environment.SpecialFolder>(specialFolder, out var folder))
            {
                return Environment.GetFolderPath(folder);
            }
            return string.Empty;
        }

        /// <summary>
        /// Combines multiple paths into a single path.
        /// </summary>
        /// <param name="paths">The paths to combine.</param>
        /// <returns>The combined path.</returns>
        public string CombinePaths(params string[] paths)
        {
            return Path.Combine(paths);
        }

        /// <summary>
        /// Gets the current directory of the application.
        /// </summary>
        /// <returns>The current directory path.</returns>
        public string GetCurrentDirectory()
        {
            return Directory.GetCurrentDirectory();
        }
    }
}
