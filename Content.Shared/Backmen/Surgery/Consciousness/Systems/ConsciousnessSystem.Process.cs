﻿using System.Linq;
using Content.Shared.Backmen.Surgery.Body.Events;
using Content.Shared.Backmen.Surgery.Consciousness.Components;
using Content.Shared.Backmen.Surgery.Pain;
using Content.Shared.Backmen.Surgery.Pain.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Mobs;

namespace Content.Shared.Backmen.Surgery.Consciousness.Systems;

public partial class ConsciousnessSystem
{
    private void InitProcess()
    {
        SubscribeLocalEvent<NerveSystemComponent, PainModifierChangedEvent>(OnPainChanged);
        SubscribeLocalEvent<NerveSystemComponent, PainModifierAddedEvent>(OnPainAdded);
        SubscribeLocalEvent<NerveSystemComponent, PainModifierRemovedEvent>(OnPainRemoved);

        SubscribeLocalEvent<ConsciousnessComponent, MobStateChangedEvent>(OnMobStateChanged);

        SubscribeLocalEvent<ConsciousnessRequiredComponent, ComponentInit>(OnConsciousnessPartInit);

        SubscribeLocalEvent<ConsciousnessRequiredComponent, BodyPartAddedEvent>(OnBodyPartAdded);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);

        SubscribeLocalEvent<ConsciousnessRequiredComponent, OrganAddedToBodyEvent>(OnOrganAdded);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);

        SubscribeLocalEvent<ConsciousnessComponent, MapInitEvent>(OnConsciousnessMapInit);
    }

    private void UpdatePassedOut(float frameTime)
    {
        var query = EntityQueryEnumerator<ConsciousnessComponent>();
        while (query.MoveNext(out var ent, out var consciousness))
        {
            if (consciousness.PassedOutTime < _timing.CurTime)
                consciousness.PassedOut = false;

            if (consciousness.ForceConsciousnessTime < _timing.CurTime)
                consciousness.ForceConscious = false;

            CheckConscious(ent, consciousness);
        }
    }

    private void OnPainChanged(EntityUid uid, NerveSystemComponent component, PainModifierChangedEvent args)
    {
        if (!TryComp<OrganComponent>(args.NerveSystem, out var nerveSysOrgan))
            return;

        if (!SetConsciousnessModifier(nerveSysOrgan.Body!.Value,
                args.NerveSystem,
                -component.Pain,
                null,
                ConsciousnessModType.Pain))
        {
            AddConsciousnessModifier(nerveSysOrgan.Body!.Value,
                args.NerveSystem,
                -component.Pain,
                null,
                "Pain",
                ConsciousnessModType.Pain);
        }
    }

    private void OnPainAdded(EntityUid uid, NerveSystemComponent component, PainModifierAddedEvent args)
    {
        if (!TryComp<OrganComponent>(args.NerveSystem, out var nerveSysOrgan))
            return;

        if (!SetConsciousnessModifier(nerveSysOrgan.Body!.Value,
                args.NerveSystem,
                -component.Pain,
                null,
                ConsciousnessModType.Pain))
        {
            AddConsciousnessModifier(nerveSysOrgan.Body!.Value,
                args.NerveSystem,
                -component.Pain,
                null,
                "Pain",
                ConsciousnessModType.Pain);
        }
    }

    private void OnPainRemoved(EntityUid uid, NerveSystemComponent component, PainModifierRemovedEvent args)
    {
        if (!TryComp<OrganComponent>(args.NerveSystem, out var nerveSysOrgan))
            return;

        if (args.CurrentPain <= 0)
        {
            RemoveConsciousnessModifer(nerveSysOrgan.Body!.Value,
                args.NerveSystem,
                type: ConsciousnessModType.Pain);
        }
        else
        {
            SetConsciousnessModifier(nerveSysOrgan.Body!.Value,
                args.NerveSystem,
                -component.Pain,
                type: ConsciousnessModType.Pain);
        }
    }

    private void OnMobStateChanged(EntityUid uid, ConsciousnessComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        AddConsciousnessModifier(uid, uid, -component.Cap, component, "DeathThreshold");
        // To prevent people from suddenly resurrecting while being dead. whoops

        foreach (var multiplier in
                 component.Multipliers.Where(multiplier => multiplier.Value.Type != ConsciousnessModType.Pain))
        {
            RemoveConsciousnessMultiplier(uid, multiplier.Key.Item1, multiplier.Value.Type, component);
        }
    }

    private void OnConsciousnessMapInit(EntityUid uid, ConsciousnessComponent consciousness, MapInitEvent args)
    {
        if (consciousness.RawConsciousness < 0)
        {
            consciousness.RawConsciousness = consciousness.Cap;
            Dirty(uid, consciousness);
        }

        CheckConscious(uid, consciousness);
    }

    private void OnConsciousnessPartInit(EntityUid uid, ConsciousnessRequiredComponent component, ComponentInit args)
    {
        if (_net.IsClient)
            return;

        EntityUid? body = null;
        if (TryComp<BodyPartComponent>(uid, out var bodyPart))
        {
            body = bodyPart.Body;
        }
        else if (TryComp<OrganComponent>(uid, out var organ))
        {
            body = organ.Body;
        }

        if (!TryComp<ConsciousnessComponent>(body, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryAdd(component.Identifier, (uid, component.CausesDeath, false)))
        {
            _sawmill.Warning($"ConsciousnessRequirementPart with duplicate Identifier {component.Identifier}:{uid} added to a body:" +
                             $" {uid} this will result in unexpected behaviour!");
        }
    }

    private void OnBodyPartAdded(EntityUid uid, ConsciousnessRequiredComponent component, ref BodyPartAddedEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.Part.Comp.Body == null ||
            !TryComp<ConsciousnessComponent>(args.Part.Comp.Body, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.ContainsKey(component.Identifier)
            && consciousness.RequiredConsciousnessParts[component.Identifier].Item1 != null)
        {
            _sawmill.Warning($"ConsciousnessRequirementPart with duplicate Identifier {component.Identifier}:{uid} added to a body:" +
                        $" {args.Part.Comp.Body} this will result in unexpected behaviour!");
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, component.CausesDeath, false);

        CheckRequiredParts(args.Part.Comp.Body.Value, consciousness);
    }

    private void OnBodyPartRemoved(EntityUid uid, ConsciousnessRequiredComponent component, ref BodyPartRemovedEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.Part.Comp.Body == null || !TryComp<ConsciousnessComponent>(args.Part.Comp.Body.Value, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value))
        {
            _sawmill.Warning($"ConsciousnessRequirementPart with identifier {component.Identifier} not found on body:{uid}");
            return;
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] =
            (uid, value.Item2, true);

        CheckRequiredParts(args.Part.Comp.Body.Value, consciousness);
    }

    private void OnOrganAdded(EntityUid uid, ConsciousnessRequiredComponent component, ref OrganAddedToBodyEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryComp<ConsciousnessComponent>(args.Body, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value) && value.Item1 != null)
        {
            _sawmill.Warning($"ConsciousnessRequirementPart with duplicate Identifier {component.Identifier}:{uid} added to a body:" +
                        $" {args.Body} this will result in unexpected behaviour!");
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid, component.CausesDeath, false);

        CheckRequiredParts(args.Body, consciousness);
    }

    private void OnOrganRemoved(EntityUid uid, ConsciousnessRequiredComponent component, ref OrganRemovedFromBodyEvent args)
    {
        if (!TryComp<ConsciousnessComponent>(args.OldBody, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryGetValue(component.Identifier, out var value))
        {
            _sawmill.Warning($"ConsciousnessRequirementPart with identifier {component.Identifier} not found on body:{uid}");
            return;
        }

        consciousness.RequiredConsciousnessParts[component.Identifier] =
            (uid, value.Item2, true);

        CheckRequiredParts(args.OldBody, consciousness);
    }
}
