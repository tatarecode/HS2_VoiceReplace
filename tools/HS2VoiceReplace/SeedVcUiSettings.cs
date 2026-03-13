using System.ComponentModel;

namespace HS2VoiceReplace;

// Defines the end-user configurable Seed-VC engine and inference parameters used by the pipeline.

internal enum SeedVcEngine
{
    V1,
    V2,
}

internal sealed class SeedVcUiSettings
{
    [LocalizedCategory("seedvc.category.engine")]
    [LocalizedDisplayName("seedvc.engine.display")]
    [LocalizedDescription("seedvc.engine.description")]
    public SeedVcEngine Engine { get; set; } = SeedVcEngine.V1;

    [LocalizedCategory("seedvc.category.basic")]
    [LocalizedDisplayName("seedvc.diffusionSteps.display")]
    [LocalizedDescription("seedvc.diffusionSteps.description")]
    public int DiffusionSteps { get; set; } = 25;

    [LocalizedCategory("seedvc.category.basic")]
    [LocalizedDisplayName("seedvc.lengthAdjust.display")]
    [LocalizedDescription("seedvc.lengthAdjust.description")]
    public double LengthAdjust { get; set; } = 1.0;

    [LocalizedCategory("seedvc.category.basic")]
    [LocalizedDisplayName("seedvc.intelligibility.display")]
    [LocalizedDescription("seedvc.intelligibility.description")]
    public double IntelligibilityCfgRate { get; set; } = 0.7;

    [LocalizedCategory("seedvc.category.basic")]
    [LocalizedDisplayName("seedvc.similarity.display")]
    [LocalizedDescription("seedvc.similarity.description")]
    public double SimilarityCfgRate { get; set; } = 0.7;

    [LocalizedCategory("seedvc.category.basic")]
    [LocalizedDisplayName("seedvc.topP.display")]
    [LocalizedDescription("seedvc.topP.description")]
    public double TopP { get; set; } = 0.9;

    [LocalizedCategory("seedvc.category.basic")]
    [LocalizedDisplayName("seedvc.temperature.display")]
    [LocalizedDescription("seedvc.temperature.description")]
    public double Temperature { get; set; } = 0.95;

    [LocalizedCategory("seedvc.category.basic")]
    [LocalizedDisplayName("seedvc.repetitionPenalty.display")]
    [LocalizedDescription("seedvc.repetitionPenalty.description")]
    public double RepetitionPenalty { get; set; } = 1.0;

    [LocalizedCategory("seedvc.category.nr")]
    [LocalizedDisplayName("seedvc.nrStylePre.display")]
    [LocalizedDescription("seedvc.nrStylePre.description")]
    public bool NrStylePre { get; set; } = false;

    [LocalizedCategory("seedvc.category.nr")]
    [LocalizedDisplayName("seedvc.nrOutPost.display")]
    [LocalizedDescription("seedvc.nrOutPost.description")]
    public bool NrOutPost { get; set; } = false;

    [LocalizedCategory("seedvc.category.nr")]
    [LocalizedDisplayName("seedvc.nrStylePropDecrease.display")]
    [LocalizedDescription("seedvc.nrStylePropDecrease.description")]
    public double NrStylePropDecrease { get; set; } = 0.6;

    [LocalizedCategory("seedvc.category.nr")]
    [LocalizedDisplayName("seedvc.nrOutPropDecrease.display")]
    [LocalizedDescription("seedvc.nrOutPropDecrease.description")]
    public double NrOutPropDecrease { get; set; } = 0.5;

    [LocalizedCategory("seedvc.category.nr")]
    [LocalizedDisplayName("seedvc.nrTimeMaskSmoothMs.display")]
    [LocalizedDescription("seedvc.nrTimeMaskSmoothMs.description")]
    public double NrTimeMaskSmoothMs { get; set; } = 40.0;

    [LocalizedCategory("seedvc.category.nr")]
    [LocalizedDisplayName("seedvc.nrFreqMaskSmoothHz.display")]
    [LocalizedDescription("seedvc.nrFreqMaskSmoothHz.description")]
    public double NrFreqMaskSmoothHz { get; set; } = 200.0;

    [LocalizedCategory("seedvc.category.harsh")]
    [LocalizedDisplayName("seedvc.harshFix.display")]
    [LocalizedDescription("seedvc.harshFix.description")]
    public bool HarshFix { get; set; } = false;

    [LocalizedCategory("seedvc.category.harsh")]
    [LocalizedDisplayName("seedvc.harshHfCutoff.display")]
    [LocalizedDescription("seedvc.harshHfCutoff.description")]
    public double HarshHfCutoff { get; set; } = 4000.0;

    [LocalizedCategory("seedvc.category.harsh")]
    [LocalizedDisplayName("seedvc.harshSrcHfMix.display")]
    [LocalizedDescription("seedvc.harshSrcHfMix.description")]
    public double HarshSrcHfMix { get; set; } = 0.65;

    [LocalizedCategory("seedvc.category.harsh")]
    [LocalizedDisplayName("seedvc.harshOverFactor.display")]
    [LocalizedDescription("seedvc.harshOverFactor.description")]
    public double HarshOverFactor { get; set; } = 1.55;

    [LocalizedCategory("seedvc.category.harsh")]
    [LocalizedDisplayName("seedvc.harshFlatnessTh.display")]
    [LocalizedDescription("seedvc.harshFlatnessTh.description")]
    public double HarshFlatnessTh { get; set; } = 0.34;

    [LocalizedCategory("seedvc.category.harsh")]
    [LocalizedDisplayName("seedvc.harshMinSegmentMs.display")]
    [LocalizedDescription("seedvc.harshMinSegmentMs.description")]
    public double HarshMinSegmentMs { get; set; } = 18.0;

    [LocalizedCategory("seedvc.category.breath")]
    [LocalizedDisplayName("seedvc.breathPassThrough.display")]
    [LocalizedDescription("seedvc.breathPassThrough.description")]
    public bool BreathPassThrough { get; set; } = false;

    [LocalizedCategory("seedvc.category.breath")]
    [LocalizedDisplayName("seedvc.breathFlatnessTh.display")]
    [LocalizedDescription("seedvc.breathFlatnessTh.description")]
    public double BreathFlatnessTh { get; set; } = 0.42;

    [LocalizedCategory("seedvc.category.breath")]
    [LocalizedDisplayName("seedvc.breathRmsMax.display")]
    [LocalizedDescription("seedvc.breathRmsMax.description")]
    public double BreathRmsMax { get; set; } = 0.22;

    [LocalizedCategory("seedvc.category.breath")]
    [LocalizedDisplayName("seedvc.breathMix.display")]
    [LocalizedDescription("seedvc.breathMix.description")]
    public double BreathMix { get; set; } = 1.0;

    [LocalizedCategory("seedvc.category.globalHf")]
    [LocalizedDisplayName("seedvc.globalHfBlend.display")]
    [LocalizedDescription("seedvc.globalHfBlend.description")]
    public bool GlobalHfBlend { get; set; } = true;

    [LocalizedCategory("seedvc.category.globalHf")]
    [LocalizedDisplayName("seedvc.globalHfCutoff.display")]
    [LocalizedDescription("seedvc.globalHfCutoff.description")]
    public double GlobalHfCutoff { get; set; } = 3500.0;

    [LocalizedCategory("seedvc.category.globalHf")]
    [LocalizedDisplayName("seedvc.globalHfSrcMix.display")]
    [LocalizedDescription("seedvc.globalHfSrcMix.description")]
    public double GlobalHfSrcMix { get; set; } = 0.30;

    [LocalizedCategory("seedvc.category.deesser")]
    [LocalizedDisplayName("seedvc.globalDeEsser.display")]
    [LocalizedDescription("seedvc.globalDeEsser.description")]
    public bool GlobalDeEsser { get; set; } = false;

    [LocalizedCategory("seedvc.category.deesser")]
    [LocalizedDisplayName("seedvc.deEsserLowHz.display")]
    [LocalizedDescription("seedvc.deEsserLowHz.description")]
    public double DeEsserLowHz { get; set; } = 6000.0;

    [LocalizedCategory("seedvc.category.deesser")]
    [LocalizedDisplayName("seedvc.deEsserHighHz.display")]
    [LocalizedDescription("seedvc.deEsserHighHz.description")]
    public double DeEsserHighHz { get; set; } = 10000.0;

    [LocalizedCategory("seedvc.category.deesser")]
    [LocalizedDisplayName("seedvc.deEsserStrength.display")]
    [LocalizedDescription("seedvc.deEsserStrength.description")]
    public double DeEsserStrength { get; set; } = 0.28;

    [LocalizedCategory("seedvc.category.audiosr")]
    [LocalizedDisplayName("seedvc.audioSrPost.display")]
    [LocalizedDescription("seedvc.audioSrPost.description")]
    public bool AudioSrPost { get; set; } = false;

    public static SeedVcUiSettings CreateDefault() => new();

    public SeedVcUiSettings Clone() => new()
    {
        Engine = Engine,
        DiffusionSteps = DiffusionSteps,
        LengthAdjust = LengthAdjust,
        IntelligibilityCfgRate = IntelligibilityCfgRate,
        SimilarityCfgRate = SimilarityCfgRate,
        TopP = TopP,
        Temperature = Temperature,
        RepetitionPenalty = RepetitionPenalty,
        NrStylePre = NrStylePre,
        NrOutPost = NrOutPost,
        NrStylePropDecrease = NrStylePropDecrease,
        NrOutPropDecrease = NrOutPropDecrease,
        NrTimeMaskSmoothMs = NrTimeMaskSmoothMs,
        NrFreqMaskSmoothHz = NrFreqMaskSmoothHz,
        HarshFix = HarshFix,
        HarshHfCutoff = HarshHfCutoff,
        HarshSrcHfMix = HarshSrcHfMix,
        HarshOverFactor = HarshOverFactor,
        HarshFlatnessTh = HarshFlatnessTh,
        HarshMinSegmentMs = HarshMinSegmentMs,
        BreathPassThrough = BreathPassThrough,
        BreathFlatnessTh = BreathFlatnessTh,
        BreathRmsMax = BreathRmsMax,
        BreathMix = BreathMix,
        GlobalHfBlend = GlobalHfBlend,
        GlobalHfCutoff = GlobalHfCutoff,
        GlobalHfSrcMix = GlobalHfSrcMix,
        GlobalDeEsser = GlobalDeEsser,
        DeEsserLowHz = DeEsserLowHz,
        DeEsserHighHz = DeEsserHighHz,
        DeEsserStrength = DeEsserStrength,
        AudioSrPost = AudioSrPost,
    };

    public string ToSummaryString() =>
        $"engine={Engine}, steps={DiffusionSteps}, len={LengthAdjust:F2}, int={IntelligibilityCfgRate:F2}, sim={SimilarityCfgRate:F2}, " +
        $"top_p={TopP:F2}, temp={Temperature:F2}, rep={RepetitionPenalty:F2}, NR={NrStylePre}/{NrOutPost}, " +
        $"harsh={HarshFix}, breath={BreathPassThrough}, HF={GlobalHfBlend}, deesser={GlobalDeEsser}, audiosr={AudioSrPost}";
}

