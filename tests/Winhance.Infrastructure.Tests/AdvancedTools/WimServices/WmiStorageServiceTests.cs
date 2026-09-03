using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools.WimServices;

// The fake returns the types the real Storage Management API returns (UInt32 numbers, UInt16 styles
// and bus types, a Char16 letter as a char that is '\0' when unset, embedded instances for method
// output; schema probed on build 26100, 2026-08-31). A mismatch here would pass these tests and
// fail on a real disk.
public class WmiStorageServiceTests
{
    private readonly FakeStorageApi _api = new();

    private WmiStorageService CreateSut() => new(_api, Mock.Of<ILogService>());

    [Fact]
    public void GetDisks_MapsWhatTheStorageApiReports()
    {
        GivenDisk(2);
        var system = GivenDisk(0);
        system["FriendlyName"] = "Samsung SSD 990";
        system["BusType"] = (ushort)17;
        system["IsSystem"] = true;

        var disks = CreateSut().GetDisks();

        disks.Should().HaveCount(2);
        disks[0].Should().Be(new RemovableDrive(2, "SanDisk Cruzer Blade", 16_008_609_792L, "USB", IsSystemDisk: false));
        disks[1].BusType.Should().Be("NVMe");
        disks[1].IsSystemDisk.Should().BeTrue();
    }

    [Fact]
    public void GetDriveLetters_SkipsPartitionsWithoutOne()
    {
        GivenPartition(2, 1, 'E');
        GivenPartition(2, 2);
        GivenPartition(3, 1, 'F');

        CreateSut().GetDriveLetters(2).Should().Equal('E');
    }

    // Measured 2026-08-27: Clear returned 0 on a super-floppy stick and left its one partition in
    // place with LargestFreeExtent at 0; deleting that partition is what frees the disk.
    [Fact]
    public void Clear_PartitionSurvivesTheClear_DeletesItAndToleratesTheAccessPathWarning()
    {
        var disk = GivenDisk(2);
        var survivor = GivenPartition(2, 1);
        survivor.OnInvoke = (method, _) => (method == "DeleteObject" ? 42000u : 0u, new FakeInstance());

        CreateSut().Clear(2);

        disk.Invocations.Select(i => i.Method).Should().Equal("Clear");
        disk.Invocations[0].Parameters.Should().Contain("RemoveData", true);
        survivor.Invocations.Select(i => i.Method).Should().Equal("DeleteObject");
    }

    [Fact]
    public void Clear_NoFreeExtentAfterwards_Throws()
    {
        GivenDisk(2, largestFreeExtent: 0);

        Action act = () => CreateSut().Clear(2);

        act.Should().Throw<InvalidOperationException>().WithMessage("*no free space*");
    }

    [Fact]
    public void EnsureMbr_DiskAlreadyMbr_TouchesNothing()
    {
        var disk = GivenDisk(2, partitionStyle: 1);

        CreateSut().EnsureMbr(2);

        disk.Invocations.Should().BeEmpty();
    }

    // Windows can initialize removable media on its own between the read and the call, so 41001
    // from Initialize is not a failure, but only when the disk really is MBR afterwards.
    [Fact]
    public void EnsureMbr_RawDiskInitializedByWindowsFirst_AcceptsItWhenTheReadBackIsMbr()
    {
        var disk = GivenDisk(2, partitionStyle: 0);
        disk.OnInvoke = (_, _) =>
        {
            disk["PartitionStyle"] = (ushort)1;
            return (41001u, new FakeInstance());
        };

        CreateSut().EnsureMbr(2);

        disk.Invocations.Select(i => i.Method).Should().Equal("Initialize");
        disk.Invocations[0].Parameters.Should().Contain("PartitionStyle", (ushort)1);
    }

    [Fact]
    public void EnsureMbr_GptDiskAndConvertStyleReturns41001_IsAFailure()
    {
        var disk = GivenDisk(2, partitionStyle: 2);
        disk.OnInvoke = (_, _) => (41001u, new FakeInstance());

        Action act = () => CreateSut().EnsureMbr(2);

        act.Should().Throw<InvalidOperationException>().WithMessage("*41001*");
        disk.Invocations.Select(i => i.Method).Should().Equal("ConvertStyle");
    }

    // IsActive is ignored on anything but MBR, which would ship a stick that boots on UEFI only
    // and never says why; a disk that will not read back as MBR is refused instead.
    [Fact]
    public void EnsureMbr_StillNotMbrAfterARefresh_Throws()
    {
        var disk = GivenDisk(2, partitionStyle: 0);

        Action act = () => CreateSut().EnsureMbr(2);

        act.Should().Throw<InvalidOperationException>().WithMessage("*partition style 0*");
        disk.Invocations.Select(i => i.Method).Should().Equal("Initialize", "Refresh");
    }

    [Fact]
    public void CreateActiveFat32Partition_StickPastTheFat32Ceiling_CapsThePartitionSize()
    {
        var disk = GivenDisk(2, largestFreeExtent: 61_530_439_680UL);
        disk.OnInvoke = (_, _) => (0u, OutputWithCreatedPartition(1));

        var number = CreateSut().CreateActiveFat32Partition(2);

        number.Should().Be(1);
        var parameters = disk.Invocations.Single().Parameters;
        parameters.Should().Contain("Size", (ulong)UsbWriteLayoutPlanner.Fat32MaxVolumeBytes);
        parameters.Should().NotContainKey("UseMaximumSize");
        parameters.Should().Contain("MbrType", (ushort)12);
        parameters.Should().Contain("IsActive", true);
        parameters.Should().NotContainKey("AssignDriveLetter");
    }

    [Fact]
    public void CreateActiveFat32Partition_SmallerStick_UsesTheWholeDisk()
    {
        var disk = GivenDisk(2, largestFreeExtent: 16_008_609_792UL);
        disk.OnInvoke = (_, _) => (0u, OutputWithCreatedPartition(1));

        CreateSut().CreateActiveFat32Partition(2);

        var parameters = disk.Invocations.Single().Parameters;
        parameters.Should().Contain("UseMaximumSize", true);
        parameters.Should().NotContainKey("Size");
    }

    [Fact]
    public void FormatFat32_FormatsTheVolumeBehindThePartition_Quick()
    {
        var partition = GivenPartition(2, 1);
        var volume = new FakeInstance();
        partition.Related["MSFT_Volume"] = [volume];

        CreateSut().FormatFat32(2, 1, "WINHANCE");

        volume.Invocations.Single().Method.Should().Be("Format");
        var parameters = volume.Invocations.Single().Parameters;
        parameters.Should().Contain("FileSystem", "FAT32");
        parameters.Should().Contain("FileSystemLabel", "WINHANCE");
        parameters.Should().Contain("Full", false);
    }

    [Fact]
    public void AssignDriveLetter_WindowsAlreadyMountedIt_AsksForNothing()
    {
        var partition = GivenPartition(2, 1, 'E');

        CreateSut().AssignDriveLetter(2, 1).Should().Be('E');

        partition.Invocations.Should().BeEmpty();
    }

    [Fact]
    public void AssignDriveLetter_NoLetterYet_AddsAnAccessPathAndReadsItBack()
    {
        var partition = GivenPartition(2, 1);
        partition.OnInvoke = (_, _) =>
        {
            partition["DriveLetter"] = 'F';
            return (0u, new FakeInstance());
        };

        CreateSut().AssignDriveLetter(2, 1).Should().Be('F');

        partition.Invocations.Single().Method.Should().Be("AddAccessPath");
        partition.Invocations.Single().Parameters.Should().Contain("AssignDriveLetter", true);
    }

    [Fact]
    public void Invoke_ApiRefuses_NamesTheApisOwnReasonAndTheCode()
    {
        var disk = GivenDisk(2);
        disk.OnInvoke = (_, _) => (40001u, new FakeInstance
        {
            ["ExtendedStatus"] = new FakeInstance { ["Message"] = "Access denied." },
        });

        Action act = () => CreateSut().Clear(2);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Access denied.*40001*");
    }

    private FakeInstance GivenDisk(int number, ushort partitionStyle = 1, ulong largestFreeExtent = 16_008_609_792UL)
    {
        var disk = new FakeInstance
        {
            ["Number"] = (uint)number,
            ["FriendlyName"] = "SanDisk Cruzer Blade",
            ["Size"] = 16_008_609_792UL,
            ["BusType"] = (ushort)7,
            ["IsSystem"] = false,
            ["PartitionStyle"] = partitionStyle,
            ["LargestFreeExtent"] = largestFreeExtent,
        };
        _api.Disks.Add(disk);
        return disk;
    }

    private FakeInstance GivenPartition(int diskNumber, int partitionNumber, char driveLetter = '\0')
    {
        var partition = new FakeInstance
        {
            ["DiskNumber"] = (uint)diskNumber,
            ["PartitionNumber"] = (uint)partitionNumber,
            ["DriveLetter"] = driveLetter,
        };
        _api.Partitions.Add(partition);
        return partition;
    }

    private static FakeInstance OutputWithCreatedPartition(uint partitionNumber) => new()
    {
        ["CreatedPartition"] = new FakeInstance { ["PartitionNumber"] = partitionNumber },
    };

    // Answers the three query shapes the service writes: no condition, "Number = n",
    // "DiskNumber = n" and "DiskNumber = n AND PartitionNumber = p".
    private sealed class FakeStorageApi : IWmiApi
    {
        public List<FakeInstance> Disks { get; } = [];

        public List<FakeInstance> Partitions { get; } = [];

        public IReadOnlyList<IWmiInstance> Query(string scope, string className, string? condition)
        {
            var all = className switch
            {
                "MSFT_Disk" => Disks,
                "MSFT_Partition" => Partitions,
                _ => throw new InvalidOperationException($"The fake knows nothing about {className}."),
            };

            if (condition is null)
            {
                return all;
            }

            var wanted = condition.Split(" AND ")
                .Select(clause => clause.Split(" = "))
                .ToDictionary(parts => parts[0].Trim(), parts => int.Parse(parts[1].Trim()));

            return all.Where(instance => wanted.All(w => Convert.ToInt32(instance.Get(w.Key)) == w.Value)).ToList();
        }

        public WmiMethodResult InvokeClassMethod(
            string scope, string className, string method, IReadOnlyDictionary<string, object>? parameters) =>
            throw new NotSupportedException("WmiStorageService does not call class methods.");
    }

    private sealed class FakeInstance : IWmiInstance
    {
        private readonly Dictionary<string, object?> _properties = new(StringComparer.Ordinal);

        public Dictionary<string, List<FakeInstance>> Related { get; } = new(StringComparer.Ordinal);

        public Func<string, IReadOnlyDictionary<string, object>?, (uint ReturnValue, FakeInstance Output)>? OnInvoke { get; set; }

        public List<(string Method, Dictionary<string, object> Parameters)> Invocations { get; } = [];

        public object? this[string property]
        {
            get => Get(property);
            set => _properties[property] = value;
        }

        public object? Get(string property) => _properties.GetValueOrDefault(property);

        public IReadOnlyList<IWmiInstance> GetRelated(string className) =>
            Related.TryGetValue(className, out var related) ? related : [];

        public WmiMethodResult Invoke(string method, IReadOnlyDictionary<string, object>? parameters)
        {
            Invocations.Add((method, parameters is null ? [] : new Dictionary<string, object>(parameters)));
            var (returnValue, output) = OnInvoke?.Invoke(method, parameters) ?? (0u, new FakeInstance());
            return new WmiMethodResult(returnValue, output);
        }

        public void Dispose()
        {
        }
    }
}
