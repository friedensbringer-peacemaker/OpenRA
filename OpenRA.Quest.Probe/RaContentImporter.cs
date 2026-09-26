#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.IO.Compression;
using System.Security.Cryptography;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Imports the official Red Alert quickinstall archive selected by the
	/// player. Original game files remain in Android's private app storage.
	/// </summary>
	static class RaContentImporter
	{
		const string ExpectedSha1 = "44241f68e69db9511db82cf83c174737ccda300b";
		const long MaximumArchiveBytes = 128L * 1024 * 1024;

		public static void Import(Stream archiveSource, string appFiles)
		{
			ArgumentNullException.ThrowIfNull(archiveSource);
			var contentParent = Path.Combine(appFiles, "Content", "ra");
			Directory.CreateDirectory(contentParent);
			var id = Guid.NewGuid().ToString("N");
			var archivePath = Path.Combine(contentParent, $".quickinstall-{id}.zip");
			var stagingPath = Path.Combine(contentParent, $".v2-import-{id}");
			var destinationPath = Path.Combine(contentParent, "v2");
			var backupPath = Path.Combine(contentParent, $".v2-backup-{id}");

			try
			{
				using (var archiveFile = File.Create(archivePath))
				using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1))
				{
					var buffer = new byte[64 * 1024];
					long written = 0;
					int read;
					while ((read = archiveSource.Read(buffer)) != 0)
					{
						written += read;
						if (written > MaximumArchiveBytes)
							throw new InvalidDataException("The selected archive is too large.");

						archiveFile.Write(buffer, 0, read);
						hash.AppendData(buffer, 0, read);
					}

					var actualSha1 = Convert.ToHexStringLower(hash.GetHashAndReset());
					if (actualSha1 != ExpectedSha1)
						throw new InvalidDataException("The selected file is not OpenRA's Red Alert quickinstall archive.");
				}

				Directory.CreateDirectory(stagingPath);
				using (var zip = ZipFile.OpenRead(archivePath))
					foreach (var entry in zip.Entries)
					{
						if (entry.FullName.EndsWith('/'))
							continue;

						var outputPath = Path.GetFullPath(Path.Combine(stagingPath, entry.FullName));
						if (!outputPath.StartsWith(stagingPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
							throw new InvalidDataException("The archive contains an invalid path.");

						Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
						entry.ExtractToFile(outputPath);
					}

				foreach (var required in new[] { "snow.mix", "conquer.mix" })
				{
					var file = new FileInfo(Path.Combine(stagingPath, required));
					if (!file.Exists || file.Length == 0)
						throw new InvalidDataException($"The archive is missing {required}.");
				}

				if (Directory.Exists(destinationPath))
					Directory.Move(destinationPath, backupPath);

				try
				{
					Directory.Move(stagingPath, destinationPath);
				}
				catch
				{
					if (Directory.Exists(backupPath))
						Directory.Move(backupPath, destinationPath);

					throw;
				}
			}
			finally
			{
				if (File.Exists(archivePath))
					File.Delete(archivePath);

				if (Directory.Exists(stagingPath))
					Directory.Delete(stagingPath, true);

				if (Directory.Exists(backupPath) && Directory.Exists(destinationPath))
					Directory.Delete(backupPath, true);
			}
		}
	}
}
