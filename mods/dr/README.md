## Notes on this mod

Please see the [wiki pages](https://github.com/drogoganor/DarkReign/wiki) for more info.

### Notes from IceReaper

On per-building upgrades:

DR has per factory upgrades, which tell the factory what it can build

RE: Per-upgrade sprites

you will need a custom SpriteBody trait, which selects the sprite by doing something like
var anim = "idle" + self.Trait<Researchable>().Level;
and thats it

public override IEnumerable<ActorInfo> BuildableItems()
        {
            if (productionTraits.Any() && productionTraits.All(p => p.IsTraitDisabled))
                return Enumerable.Empty<ActorInfo>();
            if (!Enabled)
                return Enumerable.Empty<ActorInfo>();
            if (developerMode.AllTech)
                return Producible.Keys;

            return Producible.Keys.Where(p =>
            {
                var buildable = p.TraitInfo<AdvancedBuildableInfo>();

                if (buildable.Prerequisites.Length == 0)
                    return true;

                if (!Actor.Info.TraitInfos<ProvidesPrerequisiteInfo>().Any(providesPrerequisite => buildable.Prerequisites.Contains(providesPrerequisite.Prerequisite)))
                    return false;

                return buildable.Level == 0 || buildable.Level <= researchable.Level;
            });
        }

i also have prerequisites as you can see implemented here on per-actor
thats required if you take over someone else factory, and you have both, the technology should still be limited to what that building would be able to build