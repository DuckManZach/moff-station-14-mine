using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Administration.UI;
using Content.Server.Disposal.Tube;
using Content.Server.EUI;
using Content.Server.Ghost.Roles;
using Content.Server.Mind;
using Content.Server.Prayer;
using Content.Server.Silicons.Laws;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Configurable;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Verbs;
using Robust.Server.Console;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Toolshed;
using Robust.Shared.Utility;
using System.Linq;
using static Content.Shared.Configurable.ConfigurationComponent;

namespace Content.Server.Administration.Systems
{
    /// <summary>
    ///     System to provide various global admin/debug verbs
    /// </summary>
    public sealed partial class AdminVerbSystem : EntitySystem
    {
        [Dependency] private readonly IConGroupController _groupController = default!;
        [Dependency] private readonly IConsoleHost _console = default!;
        [Dependency] private readonly IAdminManager _adminManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly SharedMapSystem _map = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly AdminSystem _adminSystem = default!;
        [Dependency] private readonly DisposalTubeSystem _disposalTubes = default!;
        [Dependency] private readonly EuiManager _euiManager = default!;
        [Dependency] private readonly GhostRoleSystem _ghostRoleSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly PrayerSystem _prayerSystem = default!;
        [Dependency] private readonly MindSystem _mindSystem = default!;
        [Dependency] private readonly ToolshedManager _toolshed = default!;
        [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly StationSystem _stations = default!;
        [Dependency] private readonly StationSpawningSystem _spawning = default!;
        [Dependency] private readonly ExamineSystemShared _examine = default!;
        [Dependency] private readonly AdminFrozenSystem _freeze = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly SiliconLawSystem _siliconLawSystem = default!;

        private readonly Dictionary<ICommonSession, List<EditSolutionsEui>> _openSolutionUis = new();

        public override void Initialize()
        {
            SubscribeLocalEvent<GetVerbsEvent<Verb>>(GetVerbs);
            SubscribeLocalEvent<RoundRestartCleanupEvent>(Reset);
            SubscribeLocalEvent<SolutionContainerManagerComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
        }

        private void GetVerbs(GetVerbsEvent<Verb> ev)
        {
            AddAdminVerbs(ev);
            AddDebugVerbs(ev);
            AddSmiteVerbs(ev);
            AddTricksVerbs(ev);
            AddAntagVerbs(ev);
        }

        private void AddAdminVerbs(GetVerbsEvent<Verb> args)
        {
            if (!TryComp(args.User, out ActorComponent? actor))
                return;

            var player = actor.PlayerSession;

            if (_adminManager.IsAdmin(player))
            {
                Verb mark = new();
                mark.Text = Loc.GetString("toolshed-verb-mark");
                mark.Message = Loc.GetString("toolshed-verb-mark-description");
                mark.Category = VerbCategory.Admin;
                mark.Act = () => _toolshed.InvokeCommand(player, "=> $marked", new List<EntityUid> {args.Target}, out _);
                mark.Impact = LogImpact.Low;
                args.Verbs.Add(mark);

                if (TryComp(args.Target, out ActorComponent? targetActor))
                {
                    // AdminHelp
                    Verb verb = new();
                    verb.Text = Loc.GetString("ahelp-verb-get-data-text");
                    verb.Category = VerbCategory.Admin;
                    verb.Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/gavel.svg.192dpi.png"));
                    verb.Act = () =>
                        _console.RemoteExecuteCommand(player, $"openahelp \"{targetActor.PlayerSession.UserId}\"");
                    verb.Impact = LogImpact.Low;
                    args.Verbs.Add(verb);

                    // Subtle Messages
                    Verb prayerVerb = new();
                    prayerVerb.Text = Loc.GetString("prayer-verbs-subtle-message");
                    prayerVerb.Category = VerbCategory.Admin;
                    prayerVerb.Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/pray.svg.png"));
                    prayerVerb.Act = () =>
                    {
                        _quickDialog.OpenDialog(player, "Subtle Message", "Message", "Popup Message", (string message, string popupMessage) =>
                        {
                            _prayerSystem.SendSubtleMessage(targetActor.PlayerSession, player, message, popupMessage == "" ? Loc.GetString("prayer-popup-subtle-default") : popupMessage);
                        });
                    };
                    prayerVerb.Impact = LogImpact.Low;
                    args.Verbs.Add(prayerVerb);

                    // Spawn - Like respawn but on the spot.
                    args.Verbs.Add(new Verb()
                    {
                        Text = Loc.GetString("admin-player-actions-spawn"),
                        Category = VerbCategory.Admin,
                        Act = () =>
                        {
                            if (!_transformSystem.TryGetMapOrGridCoordinates(args.Target, out var coords))
                            {
                                _popup.PopupEntity(Loc.GetString("admin-player-spawn-failed"), args.User, args.User);
                                return;
                            }

                            var stationUid = _stations.GetOwningStation(args.Target);

                            var profile = _gameTicker.GetPlayerProfile(targetActor.PlayerSession);
                            var mobUid = _spawning.SpawnPlayerMob(coords.Value, null, profile, stationUid);

                            if (_mindSystem.TryGetMind(args.Target, out var mindId, out var mindComp))
                                _mindSystem.TransferTo(mindId, mobUid, true, mind: mindComp);

                        },
                        ConfirmationPopup = true,
                        Impact = LogImpact.High,
                    });

                    // Clone - Spawn but without the mind transfer, also spawns at the user's coordinates not the target's
                    args.Verbs.Add(new Verb()
                    {
                        Text = Loc.GetString("admin-player-actions-clone"),
                        Category = VerbCategory.Admin,
                        Act = () =>
                        {
                            if (!_transformSystem.TryGetMapOrGridCoordinates(args.User, out var coords))
                            {
                                _popup.PopupEntity(Loc.GetString("admin-player-spawn-failed"), args.User, args.User);
                                return;
                            }

                            var stationUid = _stations.GetOwningStation(args.Target);

                            var profile = _gameTicker.GetPlayerProfile(targetActor.PlayerSession);
                            _spawning.SpawnPlayerMob(coords.Value, null, profile, stationUid);
                        },
                        ConfirmationPopup = true,
                        Impact = LogImpact.High,
                    });

                    // PlayerPanel
                    args.Verbs.Add(new Verb
                    {
                        Text = Loc.GetString("admin-player-actions-player-panel"),
                        Category = VerbCategory.Admin,
                        Act = () => _console.ExecuteCommand(player, $"playerpanel \"{targetActor.PlayerSession.UserId}\""),
                        Impact = LogImpact.Low
                    });
                }

                if (_mindSystem.TryGetMind(args.Target, out var mindId, out var mindComp) && mindComp.UserId != null)
                {
                    // Erase
                    args.Verbs.Add(new Verb
                    {
                        Text = Loc.GetString("admin-verbs-erase"),
                        Message = Loc.GetString("admin-verbs-erase-description"),
                        Category = VerbCategory.Admin,
                        Icon = new SpriteSpecifier.Texture(
                            new("/Textures/Interface/VerbIcons/delete_transparent.svg.192dpi.png")),
                        Act = () =>
                        {
                            _adminSystem.Erase(mindComp.UserId.Value);
                        },
                        Impact = LogImpact.Extreme,
                        ConfirmationPopup = true
                    });

                    // Respawn
                    args.Verbs.Add(new Verb
                    {
                        Text = Loc.GetString("admin-player-actions-respawn"),
                        Category = VerbCategory.Admin,
                        Act = () =>
                        {
                            _console.ExecuteCommand(player, $"respawn \"{mindComp.UserId}\"");
                        },
                        ConfirmationPopup = true,
                        // No logimpact as the command does it internally.
                    });

                    // Inspect mind
                    args.Verbs.Add(new Verb
                    {
                        Text = Loc.GetString("inspect-mind-verb-get-data-text"),
                        Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/sentient.svg.192dpi.png")),
                        Category = VerbCategory.Debug,
                        Act = () => _console.RemoteExecuteCommand(player, $"vv {GetNetEntity(mindId)}"),
                    });
                }

                // Freeze
                var frozen = TryComp<AdminFrozenComponent>(args.Target, out var frozenComp);
                var frozenAndMuted = frozenComp?.Muted ?? false;

                if (!frozen)
                {
                    args.Verbs.Add(new Verb
                    {
                        Priority = -1, // This is just so it doesn't change position in the menu between freeze/unfreeze.
                        Text = Loc.GetString("admin-verbs-freeze"),
                        Category = VerbCategory.Admin,
                        Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/snow.svg.192dpi.png")),
                        Act = () =>
                        {
                            EnsureComp<AdminFrozenComponent>(args.Target);
                        },
                        Impact = LogImpact.Medium,
                    });
                }

                if (!frozenAndMuted)
                {
                    // allow you to additionally mute someone when they are already frozen
                    args.Verbs.Add(new Verb
                    {
                        Priority = -1, // This is just so it doesn't change position in the menu between freeze/unfreeze.
                        Text = Loc.GetString("admin-verbs-freeze-and-mute"),
                        Category = VerbCategory.Admin,
                        Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/snow.svg.192dpi.png")),
                        Act = () =>
                        {
                            _freeze.FreezeAndMute(args.Target);
                        },
                        Impact = LogImpact.Medium,
                    });
                }

                if (frozen)
                {
                    args.Verbs.Add(new Verb
                    {
                        Priority = -1, // This is just so it doesn't change position in the menu between freeze/unfreeze.
                        Text = Loc.GetString("admin-verbs-unfreeze"),
                        Category = VerbCategory.Admin,
                        Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/snow.svg.192dpi.png")),
                        Act = () =>
                        {
                            RemComp<AdminFrozenComponent>(args.Target);
                        },
                        Impact = LogImpact.Medium,
                    });
                }


                // Admin Logs
                if (_adminManager.HasAdminFlag(player, AdminFlags.Logs))
                {
                    Verb logsVerbEntity = new()
                    {
                        Priority = -2,
                        Text = Loc.GetString("admin-verbs-admin-logs-entity"),
                        Category = VerbCategory.Admin,
                        Act = () =>
                        {
                            var ui = new AdminLogsEui();
                            _euiManager.OpenEui(ui, player);
                            ui.SetLogFilter(search:args.Target.Id.ToString());
                        },
                        Impact = LogImpact.Low
                    };
                    args.Verbs.Add(logsVerbEntity);
                }

                // TeleportTo
                args.Verbs.Add(new Verb
                {
                    Text = Loc.GetString("admin-verbs-teleport-to"),
                    Category = VerbCategory.Admin,
                    Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/open.svg.192dpi.png")),
                    Act = () =>
                    {
                        _console.ExecuteCommand(player, $"tpto {GetNetEntity(args.Target)}");
                    },
                    Impact = LogImpact.Low
                });

                // TeleportHere
                args.Verbs.Add(new Verb
                {
                    Text = Loc.GetString("admin-verbs-teleport-here"),
                    Category = VerbCategory.Admin,
                    Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/close.svg.192dpi.png")),
                    Act = () =>
                    {
                        if (HasComp<MapGridComponent>(args.Target))
                        {
                            if (player.AttachedEntity != null)
                            {
                                var mapPos = _transformSystem.GetMapCoordinates(player.AttachedEntity.Value);
                                if (TryComp(args.Target, out PhysicsComponent? targetPhysics))
                                {
                                    var offset = targetPhysics.LocalCenter;
                                    var rotation = _transformSystem.GetWorldRotation(args.Target);
                                    offset = rotation.RotateVec(offset);

                                    mapPos = mapPos.Offset(-offset);
                                }

                                _console.ExecuteCommand(player, $"tpgrid {GetNetEntity(args.Target)} {mapPos.X} {mapPos.Y} {mapPos.MapId}");
                            }
                        }
                        else
                        {
                            _console.ExecuteCommand(player, $"tpto {args.User} {args.Target}");
                        }
                    },
                    Impact = LogImpact.Low
                });

                // This logic is needed to be able to modify the AI's laws through its core and eye.
                EntityUid? target = null;
                SiliconLawBoundComponent? lawBoundComponent = null;

                if (TryComp(args.Target, out lawBoundComponent))
                {
                    target = args.Target;
                }
                // When inspecting the core we can find the entity with its laws by looking at the  AiHolderComponent.
                else if (TryComp<StationAiHolderComponent>(args.Target, out var holder) && holder.Slot.Item != null
                         && TryComp(holder.Slot.Item, out lawBoundComponent))
                {
                    target = holder.Slot.Item.Value;
                    // For the eye we can find the entity with its laws as the source of the movement relay since the eye
                    // is just a proxy for it to move around and look around the station.
                }
                else if (TryComp<MovementRelayTargetComponent>(args.Target, out var relay)
                         && TryComp(relay.Source, out lawBoundComponent))
                {
                    target = relay.Source;

                }

                if (lawBoundComponent != null && target != null && _adminManager.HasAdminFlag(player, AdminFlags.Moderator))
                {
                    args.Verbs.Add(new Verb()
                    {
                        Text = Loc.GetString("silicon-law-ui-verb"),
                        Category = VerbCategory.Admin,
                        Act = () =>
                        {
                            var ui = new SiliconLawEui(_siliconLawSystem, EntityManager, _adminManager);
                            if (!_playerManager.TryGetSessionByEntity(args.User, out var session))
                            {
                                return;
                            }
                            _euiManager.OpenEui(ui, session);
                            ui.UpdateLaws(lawBoundComponent, target.Value);
                        },
                        Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Interface/Actions/actions_borg.rsi"), "state-laws"),
                    });
                }
            }
        }

        private void AddDebugVerbs(GetVerbsEvent<Verb> args)
        {
            if (!TryComp(args.User, out ActorComponent? actor))
                return;

            var player = actor.PlayerSession;

            // Delete verb
            if (_toolshed.ActivePermissionController?.CheckInvokable(new CommandSpec(_toolshed.DefaultEnvironment.GetCommand("delete"), null), player, out _) ?? false)
            {
                Verb verb = new()
                {
                    Text = Loc.GetString("delete-verb-get-data-text"),
                    Category = VerbCategory.Debug,
                    Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/delete_transparent.svg.192dpi.png")),
                    Act = () => Del(args.Target),
                    Impact = LogImpact.Medium,
                    ConfirmationPopup = true
                };
                args.Verbs.Add(verb);
            }

            // Rejuvenate verb
            if (_toolshed.ActivePermissionController?.CheckInvokable(new CommandSpec(_toolshed.DefaultEnvironment.GetCommand("rejuvenate"), null), player, out _) ?? false)
            {
                Verb verb = new()
                {
                    Text = Loc.GetString("rejuvenate-verb-get-data-text"),
                    Category = VerbCategory.Debug,
                    Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/rejuvenate.svg.192dpi.png")),
                    Act = () => _rejuvenate.PerformRejuvenate(args.Target),
                    Impact = LogImpact.Medium
                };
                args.Verbs.Add(verb);
            }

            // Control mob verb
            if (_toolshed.ActivePermissionController?.CheckInvokable(new CommandSpec(_toolshed.DefaultEnvironment.GetCommand("mind"), "control"), player, out _) ?? false &&
                args.User != args.Target)
            {
                Verb verb = new()
                {
                    Text = Loc.GetString("control-mob-verb-get-data-text"),
                    Category = VerbCategory.Debug,
                    // TODO VERB ICON control mob icon
                    Act = () =>
                    {
                        _mindSystem.ControlMob(args.User, args.Target);
                    },
                    Impact = LogImpact.High,
                    ConfirmationPopup = true
                };
                args.Verbs.Add(verb);
            }

            // Make Sentient verb
            if (_groupController.CanCommand(player, "makesentient") &&
                args.User != args.Target &&
                !HasComp<MindContainerComponent>(args.Target))
            {
                Verb verb = new()
                {
                    Text = Loc.GetString("make-sentient-verb-get-data-text"),
                    Category = VerbCategory.Debug,
                    Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/sentient.svg.192dpi.png")),
                    Act = () => _mindSystem.MakeSentient(args.Target),
                    Impact = LogImpact.Medium
                };
                args.Verbs.Add(verb);
            }

            if (TryComp<InventoryComponent>(args.Target, out var inventoryComponent))
            {
                // Strip all verb
                if (_groupController.CanCommand(player, "stripall"))
                {
                    args.Verbs.Add(new Verb
                    {
                        Text = Loc.GetString("strip-all-verb-get-data-text"),
                        Category = VerbCategory.Debug,
                        Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/outfit.svg.192dpi.png")),
                        Act = () => _console.RemoteExecuteCommand(player, $"stripall \"{args.Target}\""),
                        Impact = LogImpact.Medium
                    });
                }

                // set outfit verb
                if (_groupController.CanCommand(player, "setoutfit"))
                {
                    Verb verb = new()
                    {
                        Text = Loc.GetString("set-outfit-verb-get-data-text"),
                        Category = VerbCategory.Debug,
                        Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/outfit.svg.192dpi.png")),
                        Act = () => _euiManager.OpenEui(new SetOutfitEui(GetNetEntity(args.Target)), player),
                        Impact = LogImpact.Medium
                    };
                    args.Verbs.Add(verb);
                }
            }

            // In range unoccluded verb
            if (_groupController.CanCommand(player, "inrangeunoccluded"))
            {
                Verb verb = new()
                {
                    Text = Loc.GetString("in-range-unoccluded-verb-get-data-text"),
                    Category = VerbCategory.Debug,
                    Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/information.svg.192dpi.png")),
                    Act = () =>
                    {

                        var message = _examine.InRangeUnOccluded(args.User, args.Target)
                            ? Loc.GetString("in-range-unoccluded-verb-on-activate-not-occluded")
                            : Loc.GetString("in-range-unoccluded-verb-on-activate-occluded");

                        _popup.PopupEntity(message, args.Target, args.User);
                    }
                };
                args.Verbs.Add(verb);
            }

            // Get Disposal tube direction verb
            if (_groupController.CanCommand(player, "tubeconnections") &&
                TryComp(args.Target, out DisposalTubeComponent? tube))
            {
                Verb verb = new()
                {
                    Text = Loc.GetString("tube-direction-verb-get-data-text"),
                    Category = VerbCategory.Debug,
                    Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/information.svg.192dpi.png")),
                    Act = () => _disposalTubes.PopupDirections(args.Target, tube, args.User)
                };
                args.Verbs.Add(verb);
            }

            // Make ghost role verb
            if (_groupController.CanCommand(player, "makeghostrole") &&
                !(EntityManager.GetComponentOrNull<MindContainerComponent>(args.Target)?.HasMind ?? false))
            {
                Verb verb = new();
                verb.Text = Loc.GetString("make-ghost-role-verb-get-data-text");
                verb.Category = VerbCategory.Debug;
                // TODO VERB ICON add ghost icon
                // Where is the national park service icon for haunted forests?
                verb.Act = () => _ghostRoleSystem.OpenMakeGhostRoleEui(player, args.Target);
                verb.Impact = LogImpact.Medium;
                args.Verbs.Add(verb);
            }

            if (_groupController.CanAdminMenu(player) &&
                TryComp(args.Target, out ConfigurationComponent? config))
            {
                Verb verb = new()
                {
                    Text = Loc.GetString("configure-verb-get-data-text"),
                    Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
                    Category = VerbCategory.Debug,
                    Act = () => _uiSystem.OpenUi(args.Target, ConfigurationUiKey.Key, actor.PlayerSession)
                };
                args.Verbs.Add(verb);
            }

            // Add verb to open Solution Editor
            if (_groupController.CanCommand(player, "addreagent") &&
                HasComp<SolutionContainerManagerComponent>(args.Target))
            {
                Verb verb = new()
                {
                    Text = Loc.GetString("edit-solutions-verb-get-data-text"),
                    Category = VerbCategory.Debug,
                    Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/spill.svg.192dpi.png")),
                    Act = () => OpenEditSolutionsEui(player, args.Target),
                    Impact = LogImpact.Medium // maybe high depending on WHAT reagents they add...
                };
                args.Verbs.Add(verb);
            }
        }

        #region SolutionsEui
        private void OnSolutionChanged(Entity<SolutionContainerManagerComponent> entity, ref SolutionContainerChangedEvent args)
        {
            foreach (var list in _openSolutionUis.Values)
            {
                foreach (var eui in list)
                {
                    if (eui.Target == entity.Owner)
                        eui.StateDirty();
                }
            }
        }

        public void OpenEditSolutionsEui(ICommonSession session, EntityUid uid)
        {
            if (session.AttachedEntity == null)
                return;

            var eui = new EditSolutionsEui(uid);
            _euiManager.OpenEui(eui, session);
            eui.StateDirty();

            if (!_openSolutionUis.ContainsKey(session)) {
                _openSolutionUis[session] = new List<EditSolutionsEui>();
            }

            _openSolutionUis[session].Add(eui);
        }

        public void OnEditSolutionsEuiClosed(ICommonSession session, EditSolutionsEui eui)
        {
            _openSolutionUis[session].Remove(eui);
            if (_openSolutionUis[session].Count == 0)
              _openSolutionUis.Remove(session);
        }

        private void Reset(RoundRestartCleanupEvent ev)
        {
            foreach (var euis in _openSolutionUis.Values)
            {
                foreach (var eui in euis.ToList())
                {
                    eui.Close();
                }
            }
            _openSolutionUis.Clear();
        }
        #endregion
    }
}
