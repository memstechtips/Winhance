using System.Collections.Generic;

namespace Winhance.Core.Interfaces.Services
{
    /// <summary>
    /// Provides access to the file system.
    /// </summary>
    public interface IFileSystemService
    {
        /// <summary>
        /// Determines whether the specified file exists.
        /// </summary>
        /// <param name="path">The file to check.</param>
        /// <returns>True if the file exists; otherwise, false.</returns>
        bool FileExists(string path);

        /// <summary>
        /// Determines whether the specified directory exists.
        /// </summary>
        /// <param name="path">The directory to check.</param>
        /// <returns>True if the directory exists; otherwise, false.</returns>
        bool DirectoryExists(string path);

        /// <summary>
        /// Creates a directory if it doesn't already exist.
        /// </summary>
        /// <param name="path">The directory to create.</param>
        /// <returns>True if the directory was created or already exists; otherwise, false.</returns>
        bool CreateDirectory(string path);

        /// <summary>
        /// Deletes a file if it exists.
        /// </summary>
        /// <param name="path">The file to delete.</param>
        /// <returns>True if the file was deleted or didn't exist; otherwise, false.</returns>
        bool DeleteFile(string path);

        /// <summary>
        /// Reads all text from a file.
        /// </summary>
        /// <param name="path">The file to read.</param>
        /// <returns>The contents of the file.</returns>
        string ReadAllText(string path);

        /// <summary>
        /// Writes all text to a file.
        /// </summary>
        /// <param name="path">The file to write to.</param>
        /// <param name="contents">The text to write.</param>
        /// <returns>True if the text was written successfully; otherwise, false.</returns>
        bool WriteAllText(string path, string contents);

        /// <summary>
        /// Gets all files in a directory that match the specified pattern.
        /// </summary>
        /// <param name="path">The directory to search.</param>
        /// <param name="pattern">The search pattern.</param>
        /// <param name="recursive">Whether to search subdirectories.</param>
        /// <returns>A collection of file paths.</returns>
        IEnumerable<string> GetFiles(string path, string pattern, bool recursive);

        /// <summary>
        /// Gets all directories in a directory that match the specified pattern.
        /// </summary>
        /// <param name="path">The directory to search.</param>
        /// <param name="pattern">The search pattern.</param>
        /// <param name="recursive">Whether to search subdirectories.</param>
        /// <returns>A collection of directory paths.</returns>
        IEnumerable<string> GetDirectories(string path, string pattern, bool recursive);

        /// <summary>
        /// Gets the path to a special folder, such as AppData, ProgramFiles, etc.
        /// </summary>
        /// <param name="specialFolder">The name of the special folder.</param>
        /// <returns>The path to the special folder.</returns>
        string GetSpecialFolderPath(string specialFolder);
    }
}