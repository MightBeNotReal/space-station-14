﻿using Content.Shared.Backmen.Surgery.Wounds.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Robust.Shared.Serialization;

namespace Content.Shared.Backmen.Surgery.Wounds;

[Serializable, NetSerializable]
public enum WoundType
{
    External,
    Internal,
}

[Serializable, NetSerializable]
public enum WoundSeverity
{
    Healed,
    Minor,
    Moderate,
    Severe,
    Critical,
    Loss,
}

[Serializable, NetSerializable]
public enum BleedingSeverity
{
    Minor,
    Severe,
}

[Serializable, NetSerializable]
public enum WoundableSeverity
{
    Minor,
    Moderate,
    Severe,
    Critical,
    Loss,
}

[Serializable, NetSerializable]
public enum WoundVisibility
{
    Always,
    HandScanner,
    AdvancedScanner,
}

[Serializable, NetSerializable]
public enum WoundableVisualizerKeys
{
    Wounds,
    Severity,
}

[Serializable, NetSerializable]
public sealed class WoundsVisualizerGroupData(List<NetEntity> woundsList) : ICloneable
{
    public List<NetEntity> WoundsList = woundsList;

    public object Clone()
    {
        return new WoundsVisualizerGroupData([..WoundsList]);
    }
}

[ByRefEvent]
public record struct WoundAddedEvent(EntityUid WoundEntity, WoundComponent Component, WoundableComponent Woundable, WoundableComponent RootWoundable);

[ByRefEvent]
public record struct WoundRemovedEvent(EntityUid WoundEntity, WoundComponent Component, WoundableComponent OldWoundable, WoundableComponent OldRootWoundable);

[ByRefEvent]
public record struct WoundableAttachedEvent(EntityUid ParentWoundableEntity, WoundableComponent Component);

[ByRefEvent]
public record struct WoundableDetachedEvent(EntityUid ParentWoundableEntity, WoundableComponent Component);

[ByRefEvent]
public record struct WoundSeverityPointChangedEvent(EntityUid WoundEntity, WoundComponent Component, FixedPoint2 OldSeverity, FixedPoint2 NewSeverity);

[ByRefEvent]
public record struct WoundSeverityChangedEvent(EntityUid WoundEntity, WoundSeverity NewSeverity);

[ByRefEvent]
public record struct WoundableIntegrityChangedEvent(EntityUid Woundable, FixedPoint2 CurrentIntegrity);

[ByRefEvent]
public record struct WoundableSeverityChangedEvent(EntityUid Woundable, WoundableSeverity NewSeverity);

[Serializable, DataRecord]
public record struct WoundableSeverityMultiplier(FixedPoint2 Change, string Identifier = "Unspecified");

[Serializable, DataRecord]
public record struct WoundableHealingMultiplier(FixedPoint2 Change, string Identifier = "Unspecified");