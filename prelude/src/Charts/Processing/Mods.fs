﻿namespace Prelude.Charts.Processing

open System
open Prelude
open Prelude.Charts

type ModdedChart =
    {
        Keys: int
        Notes: TimeArray<NoteRow>
        BPM: TimeArray<BPM>
        SV: TimeArray<float32>
        ModsSelected: Map<string, int>
        ModsApplied: Map<string, int>
    }
    member this.FirstNote = this.Notes.[0].Time
    member this.LastNote = this.Notes.[this.Notes.Length - 1].Time

type ModdedChartInternal =
    {
        Keys: int
        Notes: TimeArray<NoteRow>
        BPM: TimeArray<BPM>
        SV: TimeArray<float32>
    }

module Mirror =

    let apply (chart: ModdedChartInternal) : ModdedChartInternal * bool =
        { chart with
            Notes = TimeArray.map Array.rev chart.Notes
        },
        true

module NoSV =

    let apply (chart: ModdedChartInternal) : ModdedChartInternal * bool =
        let mutable has_sv = false

        for { Data = s } in chart.SV do
            if MathF.Round(s, 2) <> 1.0f then
                has_sv <- true

        { chart with SV = [||] }, has_sv

module NoLN =

    let apply (chart: ModdedChartInternal) : ModdedChartInternal * bool =
        let mutable has_ln = false
        let notes_copy = TimeArray.map Array.copy chart.Notes

        for { Data = nr } in notes_copy do
            for k = 0 to chart.Keys - 1 do
                match nr.[k] with
                | NoteType.HOLDHEAD ->
                    has_ln <- true
                    nr.[k] <- NoteType.NORMAL
                | NoteType.HOLDBODY -> nr.[k] <- NoteType.NOTHING
                | NoteType.HOLDTAIL -> nr.[k] <- NoteType.NOTHING
                | _ -> ()

        // todo: remove empty note rows
        { chart with Notes = notes_copy }, has_ln

module Inverse =

    // debug tool
    let diff (a: TimeArray<NoteRow>) (b: TimeArray<NoteRow>) =
        let mutable a = List.ofSeq a
        let mutable b = List.ofSeq b

        while a <> [] && b <> [] do
            let { Time = t1; Data = d1 } = a.Head
            let { Time = t2; Data = d2 } = b.Head

            if t1 < t2 then
                printfn "%s ~ " (NoteRow.pretty_print d1)
                a <- a.Tail
            elif t1 > t2 then
                printfn "     ~ %s" (NoteRow.pretty_print d2)
                b <- b.Tail
            else
                printfn "%s ~ %s" (NoteRow.pretty_print d1) (NoteRow.pretty_print d2)
                a <- a.Tail
                b <- b.Tail

        for { Data = d1 } in a do
            printfn "%s ~ " (NoteRow.pretty_print d1)

        for { Data = d2 } in b do
            printfn "     ~ %s" (NoteRow.pretty_print d2)

    let apply (halved: bool) (chart: ModdedChartInternal) : ModdedChartInternal * bool =

        let output = chart.Notes |> TimeArray.map Array.copy |> ResizeArray

        let mutable spacing = 0.0f<ms>

        let remove_hold (k: int) (start_index: int) =
            let { Data = r } = output.[start_index]
            assert (r.[k] = NoteType.HOLDHEAD)
            r.[k] <- NoteType.NORMAL

            let mutable finished = false
            let mutable i = start_index + 1

            while not finished do
                let { Data = row } = output.[i]

                if row.[k] = NoteType.HOLDTAIL then
                    row.[k] <- NoteType.NOTHING
                    finished <- true
                else
                    row.[k] <- NoteType.NOTHING

                i <- i + 1

        let add_hold (k: int) (start_index: int) =

            if start_index = output.Count - 1 then
                ()
            else

                let { Time = start_time; Data = r } = output.[start_index]
                assert (r.[k] = NoteType.NORMAL)

                // fd
                let mutable found_next_note = false
                let mutable i = start_index + 1

                while i < output.Count && not found_next_note do
                    let { Data = row } = output.[i]

                    if row.[k] = NoteType.HOLDHEAD || row.[k] = NoteType.NORMAL then
                        found_next_note <- true

                    i <- i + 1

                if not found_next_note then // end of chart
                    assert (i = output.Count)
                    let { Data = row } = output.[i - 1] in
                    row.[k] <- NoteType.HOLDTAIL

                    for x = i - 2 downto start_index + 1 do
                        let { Data = row } = output.[x]
                        row.[k] <- NoteType.HOLDBODY

                    r.[k] <- NoteType.HOLDHEAD
                else
                    let intended_tail_time = output.[i - 1].Time - spacing

                    if intended_tail_time - spacing <= start_time then
                        ()
                    else

                        let mutable tail_position = i - 2 // index before found note

                        while output.[tail_position].Time > intended_tail_time do
                            tail_position <- tail_position - 1

                        assert (output.[tail_position].Time <= intended_tail_time)

                        let { Time = time; Data = row } = output.[tail_position]

                        if time = intended_tail_time then
                            row.[k] <- NoteType.HOLDTAIL
                        else
                            let new_row = NoteRow.create_empty chart.Keys

                            for key = 0 to chart.Keys - 1 do
                                if row.[key] = NoteType.HOLDBODY || row.[key] = NoteType.HOLDHEAD then
                                    new_row.[key] <- NoteType.HOLDBODY

                            new_row.[k] <- NoteType.HOLDTAIL
                            tail_position <- tail_position + 1

                            output.Insert(
                                tail_position,
                                {
                                    Time = intended_tail_time
                                    Data = new_row
                                }
                            )

                        for x = tail_position - 1 downto start_index + 1 do
                            let { Data = row } = output.[x]
                            row.[k] <- NoteType.HOLDBODY

                        r.[k] <- NoteType.HOLDHEAD

        let mutable bpm_index = -1

        let update_spacing now =
            while bpm_index < 0
                  || (bpm_index + 1 < chart.BPM.Length && chart.BPM.[bpm_index + 1].Time < now) do
                let msPerBeat = chart.BPM.[bpm_index + 1].Data.MsPerBeat
                spacing <- msPerBeat * if halved then 0.125f<beat> else 0.25f<beat>
                bpm_index <- bpm_index + 1

        let mutable i = 0

        while i < output.Count do
            let { Time = time; Data = d } = output.[i]
            update_spacing time

            for k, nt in Array.indexed d do
                if nt = NoteType.NORMAL then
                    add_hold k i
                elif nt = NoteType.HOLDHEAD then
                    remove_hold k i

            i <- i + 1

        { chart with
            Notes = output |> Array.ofSeq
        },
        true

module MoreNotes =

    let apply_chordjacks (chart: ModdedChartInternal) : ModdedChartInternal * bool =

        if chart.Keys <> 4 then
            chart, false
        else

            let mutable previous_nr: NoteRow = Array.zeroCreate chart.Keys

            let add_note (if_jack_in_column: int) (add_to_column: int) (nr: NoteRow) =
                if
                    (nr.[if_jack_in_column] = NoteType.NORMAL
                     || nr.[if_jack_in_column] = NoteType.HOLDHEAD)
                    && previous_nr.[if_jack_in_column] = NoteType.NORMAL
                    && nr.[add_to_column] = NoteType.NOTHING
                then
                    nr.[add_to_column] <- NoteType.NORMAL

                nr

            let new_notes =
                chart.Notes
                |> TimeArray.map (fun nr ->
                    let new_notes =
                        nr
                        |> NoteRow.clone
                        |> add_note 0 1
                        |> add_note 1 0
                        |> add_note 2 3
                        |> add_note 3 2

                    previous_nr <- new_notes
                    new_notes
                )

            { chart with Notes = new_notes }, true

    let apply_minijacks (chart: ModdedChartInternal) : ModdedChartInternal * bool =

        if chart.Keys <> 4 then
            chart, false
        else

            let MS_THRESHOLD = 20.0f<ms>

            let mutable flip = false
            let mutable skip = true
            let mutable previous_nr: NoteRow = Array.zeroCreate chart.Keys
            let mutable previous_time: Time = Time.infinity

            let add_note (nr: NoteRow) =
                flip <- not flip

                match
                    if flip then
                        seq { 0 .. chart.Keys - 1 }
                    else
                        seq { chart.Keys - 1 .. -1 .. 0 }
                    |> Seq.tryFind (fun i -> previous_nr.[i] <> NoteType.NORMAL && nr.[i] = NoteType.NOTHING)
                with
                | Some i -> nr.[i] <- NoteType.NORMAL
                | None -> ()

                nr

            let new_notes =
                chart.Notes
                |> Array.map (fun { Time = time; Data = nr } ->
                    skip <- skip || (time - previous_time) < MS_THRESHOLD
                    let new_notes = nr |> NoteRow.clone |> (if not skip then add_note else id)

                    skip <- not skip
                    previous_nr <- new_notes
                    previous_time <- time
                    { Time = time; Data = new_notes }
                )

            { chart with Notes = new_notes }, true
