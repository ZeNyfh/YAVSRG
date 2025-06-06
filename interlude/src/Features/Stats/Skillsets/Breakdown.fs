﻿namespace Interlude.Features.Stats

open Percyqaz.Common
open Percyqaz.Flux.UI
open Prelude.Calculator
open Prelude.Calculator.Patterns
open Prelude.Data.User.Stats
open Interlude.UI
open Interlude.Features.Gameplay

type SkillBreakdown() =
    inherit Container(NodeType.None)

    let default_keymode = SelectedChart.keymode() |> int
    let keymode = Setting.simple default_keymode
    static let skill = Setting.simple Jacks
    static let source = Setting.simple AllTime

    let graph_container = SwapContainer()
    let refresh_graph() =
        graph_container.Current <- SkillBreakdownGraph.Create(keymode.Value, skill.Value, source.Value)

    override this.Init(parent: Widget) =
        let available_keymodes =
            seq {
                for i = 3 to 10 do
                    if TOTAL_STATS.KeymodeSkills.[i - 3] <> KeymodeSkillBreakdown.Default || i = default_keymode then
                        yield i
            }
            |> Array.ofSeq

        refresh_graph()

        let source_switcher =
            AngledButton(
                (fun () -> sprintf "%O" source.Value),
                (fun () ->
                    source.Value <- match source.Value with AllTime -> Recent | _ -> AllTime
                    refresh_graph()
                ),
                Colors.black.O2
            )
                .LeanLeft(false)

        let keymode_switcher =
            AngledButton(
                (fun () -> sprintf "%iK" keymode.Value),
                (fun () ->
                    keymode.Value <- available_keymodes.[(1 + Array.findIndex ((=) keymode.Value) available_keymodes) % available_keymodes.Length]
                    refresh_graph()
                ),
                Colors.shadow_2.O2
            )

        let skill_switcher =
            AngledButton(
                (fun () -> sprintf "%O" skill.Value),
                (fun () ->
                    skill.Value <- match skill.Value with Jacks -> Chordstream | Chordstream -> Stream | Stream -> Jacks
                    refresh_graph()
                ),
                Colors.black.O2
            )
                .LeanRight(false)

        this
            .Add(
                skill_switcher
                    .Position(Position.SliceT(AngledButton.HEIGHT).SliceR(250.0f)),
                keymode_switcher
                    .Position(Position.SliceT(AngledButton.HEIGHT).SliceR(250.0f + AngledButton.LEAN_AMOUNT, 100.0f)),
                source_switcher
                    .Position(Position.SliceT(AngledButton.HEIGHT).ShrinkR(250.0f + 100.0f + AngledButton.LEAN_AMOUNT * 2.0f)),

                graph_container
                    .Position(Position.ShrinkT(AngledButton.HEIGHT))
            )
        base.Init parent

    member this.Switch(_keymode: int, _source: GraphSource, _skill: CorePattern) =
        keymode.Value <- _keymode
        source.Value <- _source
        skill.Value <- _skill
        refresh_graph()