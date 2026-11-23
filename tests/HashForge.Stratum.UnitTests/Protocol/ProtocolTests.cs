using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HashForge.Bitcoin.Primitives;
using HashForge.Bitcoin.Rpc;
using HashForge.Domain;
using HashForge.Mining.Jobs;
using HashForge.Stratum.Handlers;
using HashForge.Stratum.Protocol;

namespace HashForge.Stratum.UnitTests.Protocol;

public sealed class ProtocolTests
{
    [Fact]
    public void GIVEN_TryParse_WHEN_LineIsValidRequest_THEN_IdMethodAndParamsAreRead()
    {
        var parsed = StratumRequest.TryParse("""{"id":7,"method":"mining.submit","params":["a",1.5,null]}""", out var request);

        Assert.True(parsed);
        Assert.Equal(7, request!.Id!.Value.GetInt32());
        Assert.Equal("mining.submit", request.Method);
        Assert.Equal("a", request.GetString(0));
        Assert.Equal(1.5, request.GetNumber(1));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("""{"id":1}""")]
    [InlineData("""{"id":1,"method":5}""")]
    public void GIVEN_TryParse_WHEN_LineIsNotARequest_THEN_ReturnsFalse(string line)
    {
        Assert.False(StratumRequest.TryParse(line, out var request));
        Assert.Null(request);
    }

    [Fact]
    public void GIVEN_TryParse_WHEN_IdAndParamsAreMissing_THEN_IdIsNullAndParamsEmpty()
    {
        Assert.True(StratumRequest.TryParse("""{"method":"x","params":{}}""", out var request));

        Assert.Null(request!.Id);
        Assert.Empty(request.Parameters);
    }

    [Fact]
    public void GIVEN_GetStringAndGetNumber_WHEN_IndexMissingOrWrongType_THEN_ReturnNull()
    {
        var request = StratumTestKit.Request("x", "text", 5);

        Assert.Null(request.GetString(1));
        Assert.Null(request.GetString(9));
        Assert.Null(request.GetNumber(0));
        Assert.Null(request.GetNumber(9));
    }

    [Fact]
    public void GIVEN_Response_WHEN_Serialised_THEN_HasIdResultAndNullError()
    {
        var json = StratumMessages.Response(StratumTestKit.Json("7"), true);

        Assert.Equal("""{"id":7,"result":true,"error":null}""", json);
    }

    [Fact]
    public void GIVEN_Error_WHEN_Serialised_THEN_HasNullResultAndErrorTriple()
    {
        var json = StratumMessages.Error(StratumTestKit.Json("\"abc\""), StratumError.DuplicateShare);

        Assert.Equal("""{"id":"abc","result":null,"error":[22,"Duplicate share",null]}""", json);
    }

    [Fact]
    public void GIVEN_Notification_WHEN_Serialised_THEN_HasNullIdMethodAndParams()
    {
        Assert.Equal("""{"id":null,"method":"mining.set_difficulty","params":[512]}""", JobNotification.SetDifficulty(512));
    }

    [Fact]
    public void GIVEN_Notify_WHEN_Serialised_THEN_ContainsAllNineStratumFieldsInOrder()
    {
        var template = new BlockTemplate
        {
            Version = 0x20000000,
            PreviousBlockHash = "0000000011111111222222223333333344444444555555556666666677777777",
            Bits = "1d00ffff",
            CoinbaseValue = 1,
            CurrentTime = 0x65000000,
            Height = 100,
            Transactions = [new BlockTemplateTransaction("00", new string('a', 64))],
        };
        var coinbase = new CoinbaseTransaction([0x01, 0x02], [0x03], 12, false);
        var job = new MiningJob("1f", StratumTestKit.Pool(), template, coinbase, []);

        using var document = JsonDocument.Parse(JobNotification.Notify(job, cleanJobs: true));
        var parameters = document.RootElement.GetProperty("params");

        Assert.Equal("mining.notify", document.RootElement.GetProperty("method").GetString());
        Assert.Equal(9, parameters.GetArrayLength());
        Assert.Equal("1f", parameters[0].GetString());
        Assert.Equal("7777777766666666555555554444444433333333222222221111111100000000", parameters[1].GetString());
        Assert.Equal("0102", parameters[2].GetString());
        Assert.Equal("03", parameters[3].GetString());
        Assert.Equal(new string('a', 64), parameters[4][0].GetString());
        Assert.Equal("20000000", parameters[5].GetString());
        Assert.Equal("1d00ffff", parameters[6].GetString());
        Assert.Equal("65000000", parameters[7].GetString());
        Assert.True(parameters[8].GetBoolean());
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("bc1qabc.rig1", "bc1qabc.rig1")]
    [InlineData("evil\r\nINFO forged", "evil??INFO forged")]
    public void GIVEN_Clean_WHEN_GivenMinerText_THEN_ControlCharactersAreReplaced(string? input, string expected)
    {
        Assert.Equal(expected, LogText.Clean(input));
    }

    [Fact]
    public void GIVEN_Clean_WHEN_TextIsVeryLong_THEN_ItIsTruncated()
    {
        var cleaned = LogText.Clean(new string('x', 500));

        Assert.Equal(131, cleaned.Length);
        Assert.EndsWith("...", cleaned);
    }

    private static List<ValidationResult> ValidateOptions(StratumOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void GIVEN_StratumOptions_WHEN_Defaults_THEN_AreValidAndMaskParses()
    {
        var options = new StratumOptions();

        Assert.Empty(ValidateOptions(options));
        Assert.Equal(0x1fffe000u, options.VersionRollingMaskValue);
    }

    [Fact]
    public void GIVEN_StratumOptions_WHEN_DifficultyBoundsAreInconsistent_THEN_ValidationFails()
    {
        var options = new StratumOptions { MinDifficulty = 10, MaxDifficulty = 5, InitialDifficulty = 7 };

        Assert.Contains(ValidateOptions(options), r => r.ErrorMessage!.Contains("MinDifficulty must not exceed"));
    }

    [Fact]
    public void GIVEN_StratumOptions_WHEN_InitialDifficultyOutsideBounds_THEN_ValidationFails()
    {
        var options = new StratumOptions { MinDifficulty = 10, MaxDifficulty = 20, InitialDifficulty = 5 };

        Assert.Contains(ValidateOptions(options), r => r.ErrorMessage!.Contains("InitialDifficulty"));
    }

    [Fact]
    public void GIVEN_StratumOptions_WHEN_MaskIsNotHex_THEN_ValidationFails()
    {
        Assert.NotEmpty(ValidateOptions(new StratumOptions { VersionRollingMask = "xyz" }));
    }

    [Fact]
    public void GIVEN_ToWire_WHEN_Called_THEN_ReturnsCodeMessageNull()
    {
        Assert.Equal([21, "Job not found", null], StratumError.JobNotFound.ToWire());
        Assert.Equal(PayoutScheme.Solo, StratumTestKit.Pool().Scheme);
    }
}
