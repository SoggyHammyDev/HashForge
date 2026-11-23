using HashForge.Bitcoin.Primitives;
using HashForge.Domain;
using HashForge.Mining.Jobs;
using HashForge.Mining.Shares;

namespace HashForge.Mining.UnitTests.Shares;

public sealed class ShareValidatorTests
{
    /// <summary>Low enough that a matching nonce is found within a few hundred hashes.</summary>
    private const double EasyDifficulty = 1e-7;

    private const int MaxNonceSearch = 200_000;

    private readonly ShareValidator _validator = new();

    /// <summary>Mines until a submission satisfies the predicate, exactly as a miner would.</summary>
    private (ShareSubmission Submission, ShareValidation Validation) Mine(
        MiningJob job,
        Func<ShareValidation, bool> wanted,
        Func<uint, ShareSubmission>? build = null)
    {
        build ??= nonce => TestData.Submission(job, nonce, EasyDifficulty);
        for (uint nonce = 0; nonce < MaxNonceSearch; nonce++)
        {
            var submission = build(nonce);
            var validation = _validator.Validate(submission, job);
            if (wanted(validation))
            {
                return (submission, validation);
            }
        }

        throw new InvalidOperationException("No matching nonce found.");
    }

    [Fact]
    public void GIVEN_Validate_WHEN_HashMeetsShareDifficultyButNotNetwork_THEN_AcceptedWithoutBlock()
    {
        var job = TestData.Job();

        var (_, validation) = Mine(job, v => v.Outcome == ShareOutcome.Accepted);

        Assert.Equal(EasyDifficulty, validation.CreditedDifficulty);
        Assert.True(validation.ActualDifficulty >= EasyDifficulty);
        Assert.Null(validation.Block);
    }

    [Fact]
    public void GIVEN_Validate_WHEN_HashDoesNotMeetShareDifficulty_THEN_RejectedAsLowDifficultyWithActualDifficulty()
    {
        var job = TestData.Job();

        var (_, validation) = Mine(job, v => v.Outcome == ShareOutcome.LowDifficulty);

        Assert.Equal(0, validation.CreditedDifficulty);
        Assert.InRange(validation.ActualDifficulty, double.Epsilon, EasyDifficulty);
    }

    [Fact]
    public void GIVEN_Validate_WHEN_HashMeetsOnlyPreviousDifficulty_THEN_AcceptedAtPreviousDifficulty()
    {
        var job = TestData.Job();

        var (_, validation) = Mine(
            job,
            v => v.Outcome == ShareOutcome.Accepted,
            nonce => TestData.Submission(job, nonce, difficulty: 1e9, previousDifficulty: EasyDifficulty));

        Assert.Equal(EasyDifficulty, validation.CreditedDifficulty);
    }

    [Fact]
    public void GIVEN_Validate_WHEN_SameShareSubmittedTwice_THEN_SecondIsDuplicate()
    {
        var job = TestData.Job();
        var (submission, _) = Mine(job, v => v.Outcome == ShareOutcome.Accepted);

        var second = _validator.Validate(submission, job);

        Assert.Equal(ShareOutcome.Duplicate, second.Outcome);
    }

    [Fact]
    public void GIVEN_Validate_WHEN_HashMeetsNetworkTarget_THEN_BlockIsSerialisedWithMatchingHash()
    {
        var template = TestData.Template(TestData.RegtestBits, transactionCount: 2, witnessCommitment: "6a24aa21a9ed");
        var job = TestData.Job(template: template);

        var (submission, validation) = Mine(job, v => v.Block is not null);

        var block = Convert.FromHexString(validation.Block!.Hex);
        var header = block.AsSpan(0, BlockSerializer.HeaderSize);
        Assert.Equal(validation.Block.Hash, HashHex.ToDisplayHex(Sha256d.Hash(header)));
        Assert.True(DifficultyMath.HashToInteger(Sha256d.Hash(header)) <= job.NetworkTarget);
        Assert.Equal(3, block[BlockSerializer.HeaderSize]);

        byte[] extranonce = [.. TestData.Extranonce1, .. Convert.FromHexString(submission.Extranonce2Hex)];
        var coinbase = job.Coinbase.AssembleForBlock(extranonce);
        Assert.Equal(coinbase, block.AsSpan(BlockSerializer.HeaderSize + 1, coinbase.Length).ToArray());
        Assert.EndsWith("01ff" + "02ff", validation.Block!.Hex);

        var expectedRoot = MerkleBranch.ComputeRoot(job.Coinbase.ComputeTransactionId(extranonce), job.MerkleBranch);
        Assert.Equal(expectedRoot, header.Slice(36, 32).ToArray());
    }

    [Fact]
    public void GIVEN_Validate_WHEN_VersionBitsWithinMask_THEN_HeaderVersionCombinesTemplateAndRolledBits()
    {
        var job = TestData.Job(template: TestData.Template(TestData.RegtestBits));

        var (_, validation) = Mine(
            job,
            v => v.Block is not null,
            nonce => TestData.Submission(job, nonce, EasyDifficulty, versionBitsHex: "00004000", versionMask: 0x1fffe000));

        Assert.StartsWith("00400020", validation.Block!.Hex);
    }

    public static TheoryData<string, string?, string?, string?> MalformedFields() => new()
    {
        { "extranonce2 too short", "00", null, null },
        { "extranonce2 not hex", "zzzzzzzzzzzzzzzz", null, null },
        { "time wrong length", null, "abc", null },
        { "time before minimum", null, "00000001", null },
        { "time too far ahead", null, "ffffffff", null },
        { "version bits outside mask", null, null, "00000001" },
        { "version bits not hex", null, null, "zzzzzzzz" },
    };

    [Theory]
    [MemberData(nameof(MalformedFields))]
    public void GIVEN_Validate_WHEN_FieldIsMalformed_THEN_RejectedAsMalformed(string reason, string? extranonce2, string? time, string? versionBits)
    {
        var job = TestData.Job();
        var submission = TestData.Submission(
            job,
            extranonce2Hex: extranonce2 ?? "0000000000000001",
            timeHex: time,
            versionBitsHex: versionBits,
            versionMask: 0x1fffe000);

        var validation = _validator.Validate(submission, job);

        Assert.True(validation.Outcome == ShareOutcome.Malformed, reason);
    }

    [Fact]
    public void GIVEN_Validate_WHEN_NonceIsMalformed_THEN_RejectedAsMalformed()
    {
        var job = TestData.Job();
        var submission = TestData.Submission(job) with { NonceHex = "12" };

        Assert.Equal(ShareOutcome.Malformed, _validator.Validate(submission, job).Outcome);
    }

    [Fact]
    public void GIVEN_Validate_WHEN_Extranonce1LeavesNoRoomForExtranonce2_THEN_RejectedAsMalformed()
    {
        var job = TestData.Job();
        var submission = TestData.Submission(job) with { Extranonce1 = new byte[12], Extranonce2Hex = "" };

        Assert.Equal(ShareOutcome.Malformed, _validator.Validate(submission, job).Outcome);
    }

    [Fact]
    public void GIVEN_Rejected_WHEN_Created_THEN_HasNoCreditAndNoBlock()
    {
        var rejected = ShareValidation.Rejected(ShareOutcome.StaleJob);

        Assert.Equal(new ShareValidation(ShareOutcome.StaleJob, 0, 0), rejected);
        Assert.Null(rejected.Block);
    }

    [Theory]
    [InlineData(null, "00000000")]
    [InlineData("00000000", null)]
    public void GIVEN_Validate_WHEN_SameWorkIsResubmittedWithVersionBitsSpelledDifferently_THEN_SecondIsDuplicate(string? first, string? second)
    {
        var job = TestData.Job();
        var (submission, _) = Mine(
            job,
            v => v.Outcome == ShareOutcome.Accepted,
            nonce => TestData.Submission(job, nonce, EasyDifficulty, versionBitsHex: first));

        var replay = _validator.Validate(submission with { VersionBitsHex = second }, job);

        Assert.Equal(ShareOutcome.Duplicate, replay.Outcome);
    }

    [Fact]
    public void GIVEN_Validate_WHEN_SameWorkIsResubmittedWithDifferentHexCase_THEN_SecondIsDuplicate()
    {
        var job = TestData.Job();
        var (submission, _) = Mine(
            job,
            v => v.Outcome == ShareOutcome.Accepted,
            nonce => TestData.Submission(job, nonce + 0xa0000000, EasyDifficulty));

        var replay = _validator.Validate(submission with { NonceHex = submission.NonceHex.ToUpperInvariant() }, job);

        Assert.Equal(ShareOutcome.Duplicate, replay.Outcome);
    }

    [Fact]
    public void GIVEN_Validate_WHEN_HashSolvesBlockButMissesTheMinersShareDifficulty_THEN_BlockIsStillAccepted()
    {
        var job = TestData.Job(template: TestData.Template(TestData.RegtestBits));

        var (_, validation) = Mine(
            job,
            v => v.Outcome != ShareOutcome.Malformed,
            nonce => TestData.Submission(job, nonce, difficulty: 1e12));

        Assert.Equal(ShareOutcome.Accepted, validation.Outcome);
        Assert.NotNull(validation.Block);
        Assert.Equal(1e12, validation.CreditedDifficulty);
    }

    [Fact]
    public void GIVEN_Validate_WHEN_ManyLowDifficultySharesAreSubmitted_THEN_TheyAreNotRememberedAsDuplicates()
    {
        var job = TestData.Job();
        var (submission, first) = Mine(job, v => v.Outcome == ShareOutcome.LowDifficulty);

        var again = _validator.Validate(submission, job);

        Assert.Equal(ShareOutcome.LowDifficulty, first.Outcome);
        Assert.Equal(ShareOutcome.LowDifficulty, again.Outcome);
    }
}