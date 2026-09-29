using System.Collections.Generic;
using System.IO;
using Baryonyx.Editor.Art;
using NUnit.Framework;

namespace Baryonyx.Tests.EditMode
{
    public sealed class AsepriteDriveSyncTests
    {
        private string root;
        private string source;
        private string destination;
        private string backup;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(
                Path.GetTempPath(),
                "baryonyx-drive-sync-" + Path.GetRandomFileName()
            );
            source = Path.Combine(root, "source");
            destination = Path.Combine(root, "destination");
            backup = Path.Combine(root, "backup");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(root, true);

        [Test]
        public void CopiesNewFilesAndRecordsThem()
        {
            Write(source, "UI/Home/IconParty.aseprite", "a");
            var hashes = new Dictionary<string, string>();

            var copied = Sync(hashes);

            Assert.That(copied, Is.EqualTo(new[] { "UI/Home/IconParty.aseprite" }));
            Assert.That(Read(destination, "UI/Home/IconParty.aseprite"), Is.EqualTo("a"));
            Assert.That(hashes.ContainsKey("UI/Home/IconParty.aseprite"), Is.True);
            Assert.That(Sync(hashes), Is.Empty);
        }

        [Test]
        public void OverwritesOnlyWhenTheDestinationIsUnchangedSinceTheLastCopy()
        {
            Write(source, "IconRune.aseprite", "a");
            var hashes = new Dictionary<string, string>();
            Sync(hashes);

            Write(source, "IconRune.aseprite", "b");
            Assert.That(Sync(hashes), Is.EqualTo(new[] { "IconRune.aseprite" }));
            Assert.That(Read(destination, "IconRune.aseprite"), Is.EqualTo("b"));
        }

        [Test]
        public void KeepsBothEditsUnlessTheConflictIsOverwritten()
        {
            Write(source, "IconRune.aseprite", "a");
            var hashes = new Dictionary<string, string>();
            Sync(hashes);
            Write(source, "IconRune.aseprite", "pc");
            Write(destination, "IconRune.aseprite", "tablet");

            var plan = AsepriteDriveSync.MakePlan(source, destination, hashes);
            Assert.That(plan.Conflicts, Is.EqualTo(new[] { "IconRune.aseprite" }));
            Assert.That(Apply(plan, false, hashes), Is.Empty);
            Assert.That(Read(destination, "IconRune.aseprite"), Is.EqualTo("tablet"));

            Assert.That(Apply(plan, true, hashes), Is.EqualTo(new[] { "IconRune.aseprite" }));
            Assert.That(Read(destination, "IconRune.aseprite"), Is.EqualTo("pc"));
            Assert.That(Read(backup, "IconRune.aseprite"), Is.EqualTo("tablet"));
        }

        [Test]
        public void TreatsDifferentFilesWithoutARecordAsConflicts()
        {
            Write(source, "IconRune.aseprite", "a");
            Write(destination, "IconRune.aseprite", "b");
            Write(destination, "IconNew.aseprite", "c");

            var plan = AsepriteDriveSync.MakePlan(
                source,
                destination,
                new Dictionary<string, string>()
            );

            Assert.That(plan.Conflicts, Is.EqualTo(new[] { "IconRune.aseprite" }));
            Assert.That(plan.DestinationOnly, Is.EqualTo(new[] { "IconNew.aseprite" }));
        }

        [Test]
        public void SavesAndReadsTheRecord()
        {
            var path = Path.Combine(root, "ArtSource-sync.tsv");
            var hashes = new Dictionary<string, string> { ["UI/Home/IconParty.aseprite"] = "ab" };

            AsepriteDriveSync.WriteBase(path, hashes);

            Assert.That(AsepriteDriveSync.ReadBase(path), Is.EqualTo(hashes));
        }

        [Test]
        public void KeepsTheDriveFolderPerMachine()
        {
            var settings = Path.Combine(root, "UserSettings", "BaryonyxAsepriteDrive.json");
            Assert.That(AsepriteDriveSync.ReadDriveDirectory(settings), Is.Null);

            AsepriteDriveSync.WriteDriveDirectory(settings, destination);

            Assert.That(
                AsepriteDriveSync.ReadDriveDirectory(settings),
                Is.EqualTo(Path.GetFullPath(destination))
            );
        }

        private List<string> Sync(Dictionary<string, string> hashes) =>
            Apply(AsepriteDriveSync.MakePlan(source, destination, hashes), false, hashes);

        private List<string> Apply(
            AsepriteDriveSync.Plan plan,
            bool overwrite,
            Dictionary<string, string> hashes
        ) => AsepriteDriveSync.Apply(plan, source, destination, overwrite, backup, hashes);

        private static void Write(string directory, string name, string text)
        {
            var path = Path.Combine(directory, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        private static string Read(string directory, string name) =>
            File.ReadAllText(Path.Combine(directory, name));
    }
}
