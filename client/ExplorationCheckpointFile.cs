using System.Security.Cryptography;
using System.Text;
using NeoWizardry.Core;

// No automatic saves: a failed read can never overwrite the user's checkpoint.
public static class ExplorationCheckpointFile
{
    public static string ContentHash(string worldPath)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach(var suffix in new[] { ".glb", ".nwcol", ".nwobjects.json", ".nwdoors.json", ".nwdoors.glb", ".nwprops.json", ".nwprops.glb" })
        {
            hash.AppendData(Encoding.UTF8.GetBytes(suffix));
            var path = suffix == ".glb" ? worldPath : Path.ChangeExtension(worldPath, suffix);
            bool exists = File.Exists(path);
            hash.AppendData(new byte[] { exists ? (byte)1 : (byte)0 });
            if(exists)
            {
                using var stream = File.OpenRead(path);
                hash.AppendData(SHA256.HashData(stream));
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static string Read(string path, int maximumBytes = ExplorationCheckpoint.MaximumBytes)
    {
        using var stream = File.OpenRead(path);
        if(stream.Length > maximumBytes)
            throw new InvalidDataException("Exploration checkpoint exceeds size limit");
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
        return reader.ReadToEnd();
    }

    public static void Write(string path, string json, int maximumBytes = ExplorationCheckpoint.MaximumBytes)
    {
        var bytes = new UTF8Encoding(false, true).GetBytes(json);
        if(bytes.Length > maximumBytes)
            throw new InvalidDataException("Exploration checkpoint exceeds size limit");
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".checkpoint-{Guid.NewGuid():N}.tmp");
        try
        {
            using(var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, path, overwrite:true);
        }
        finally
        {
            if(File.Exists(temporary))File.Delete(temporary);
        }
    }
}
