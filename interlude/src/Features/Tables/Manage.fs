﻿namespace Interlude.Features.Tables

open Percyqaz.Common
open Percyqaz.Flux.UI
open Percyqaz.Flux.Graphics
open Prelude.Data.Charts.Tables
open Interlude.Content
open Interlude.Options
open Interlude.Utils
open Interlude.UI
open Interlude.UI.Menu

type private TableButton(name, action) =
    inherit
        StaticContainer(
            NodeType.Button(fun _ ->
                Style.click.Play()
                action ()
            )
        )

    override this.Init(parent: Widget) =
        this
        |+ Text(
            K(sprintf "%s  >" name),
            Color =
                (fun () ->
                    ((if this.Focused then
                          Palette.color (255, 1.0f, 0.5f)
                      else
                          Colors.white),
                     (if Some name = options.Table.Value then
                          Palette.color (255, 0.5f, 0.0f)
                      else
                          Colors.black))
                ),
            Align = Alignment.LEFT,
            Position = Position.Margin Style.PADDING
        )
        |* Clickable.Focus this

        base.Init parent

    override this.OnFocus() =
        Style.hover.Play()
        base.OnFocus()

    override this.Draw() =
        if this.Focused then
            Draw.rect this.Bounds (!*Palette.HOVER)

        base.Draw()

type ManageTablesPage(table_changed) as this =
    inherit Page()

    let container = FlowContainer.Vertical<Widget>(PRETTYHEIGHT)

    let rec refresh () =
        container.Clear()

        container
        |+ PageButton(
            "tables.install",
            (fun () ->
                Menu.Exit()
                Interlude.Features.Import.ImportScreen.switch_to_tables ()
                Screen.change Screen.Type.Import Transitions.Flags.Default |> ignore
            ),
            Icon = Icons.DOWNLOAD
        )
        |* Dummy()

        for e in Tables.list() do
            container
            |* TableButton(
                e.Info.Name,
                fun () ->
                    options.Table.Set(Some e.Id)

                    table_changed()

                    sync refresh
            )

        match Content.Table with
        | Some table ->
            container 
            |+ Dummy()
            |* PageButton("table.suggestions", (fun () -> SuggestionsPage(table).Show()))
        | None -> ()

        if container.Focused then
            container.Focus()

    do
        refresh ()

        this.Content(ScrollContainer(container, Position = Position.Margin(100.0f, 200.0f)))

    override this.Title = %"table.name"
    override this.OnClose() = ()
    override this.OnReturnTo() = refresh ()