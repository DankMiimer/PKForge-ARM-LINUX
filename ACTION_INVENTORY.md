# v3.1.1 portable action inventory

Generated from the pinned engine contracts and the portable advanced services. Each overload has a row. This is a source-reference audit, not proof of UI reachability, capability correctness or device acceptance. `Review` rows require a human decision; a reference can be indirect infrastructure or a helper rather than a menu action. Page-level workflows and hardware gates are tracked in FEATURE_COVERAGE.md.

| Contract / service action | Linux / shared source references | Status |
|---|---|---|
| `ISaveEngineSession.ApplyCosmeticEdit(Int32 box, Int32 slot, CosmeticEdit edit)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.ApplyEdit(Int32 box, Int32 slot, EntityEdit edit)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/ReferenceTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.ApplyMetEdit(Int32 box, Int32 slot, MetEdit edit)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.ApplyMoveDetails(Int32 box, Int32 slot, MoveDetailsEdit edit)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.ApplyMoveShopEdit(Int32 box, Int32 slot, MoveShopEdit edit)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.ApplyPotentialEdit(Int32 box, Int32 slot, PotentialEdit edit)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.AwardAllObtainableRibbons(Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.BatchApply(IReadOnlyList instructions, IReadOnlyList boxes)` | — | Equivalent: native batch uses BatchApplySlots with an explicit protected scope. |
| `ISaveEngineSession.BatchApplySlots(IReadOnlyList slots, IReadOnlyList instructions)` | `src/PKForge.Linux/Workspace/OrganizationTools.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.ClearBox(Int32 box)` | — | Equivalent: Empty box uses protected releases, retained recovery copies and Wilds records. |
| `ISaveEngineSession.CompleteDex()` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.DeleteBox(Int32 box)` | `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.DuplicateSlot(Int32 fromBox, Int32 fromSlot, Int32 toBox, Int32 toSlot)` | `src/PKForge.Linux/Workspace/OrganizationTools.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.ExportBoxShowdown(Int32 box)` | `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.ExportSlot(Int32 box, Int32 slot)` | `src/PKForge.Linux/ParkLibrary.cs`, `src/PKForge.Linux/Workspace/ExchangeTools.cs`, `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Linux/Workspace/Transfers.cs`, `src/PKForge.Application/PokemonWorkflow.cs`, `src/PKForge.Application/SaveOperations.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetAbilityChoices(Int32 species, Int32 form)` | `src/PKForge.Linux/Workspace/CreationTools.cs`, `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetAffixedRibbon(Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetBag()` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetBaseStats(Int32 species)` | — | Internal helper: reused MonSummaryService and MonInfoService supply these values. |
| `ISaveEngineSession.GetBoxName(Int32 box)` | `src/PKForge.Linux/Workspace/StorageScreen.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetCompassSettings()` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetCosmetics(Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetDaycare()` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetDexEntry(Int32 species)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetDexProgress()` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetEncounterCards(Int32 species, Int32 form)` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetFormChoices(Int32 species)` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetGrandUndergroundItems()` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetItemNames()` | `src/PKForge.Linux/Workspace/OrganizationTools.cs`, `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetLanguageChoices(Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetLocationChoices(Int32 box, Int32 slot, Boolean egg)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetMetInfo(Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Application/AuditWorkflow.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetMissingSpecies()` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetMoveDetails(Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetMoveShop(Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetMysteryGiftInbox()` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetNuzlockeReport()` | `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetObtainableRibbonMaxima(Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetPokeBeans()` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetPokerus(Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetPotential(Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetPouchLegalItems(String pouchName)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetRibbons(Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetRngInfo(Int32 box, Int32 slot)` | `src/PKForge.Linux/ParkLibrary.cs`, `src/PKForge.Linux/Program.cs`, `src/PKForge.Linux/Workspace/Autopilot.cs`, `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Linux/Workspace/Transfers.cs`, `src/PKForge.Application/AuditWorkflow.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetShowdownText(Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/ExchangeTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetSpeciesTypes(Int32 species)` | — | Internal helper: reused MonInfoService supplies species/type cards. |
| `ISaveEngineSession.GetStylePoints()` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetTeraTypeChoices()` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetTrainer()` | `src/PKForge.Linux/ParkLibrary.cs`, `src/PKForge.Linux/Workspace/CreationTools.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetTrainerRecords()` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetTrainerStats()` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetTrainingCaps()` | `src/PKForge.Linux/Workspace/OrganizationTools.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.GetVersionChoices()` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.ImportSlot(Int32 box, Int32 slot, Byte[] fileBytes, String format)` | `src/PKForge.Linux/ParkLibrary.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.IsDexSpecies(Int32 species)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.MakeActive()` | — | Infrastructure: SaveSessionService calls this when opening/reopening a save. |
| `ISaveEngineSession.MakeMine(Int32 box, Int32 slot, TrainerProfile profile)` | `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.MoveSlot(Int32 fromBox, Int32 fromSlot, Int32 toBox, Int32 toSlot)` | `src/PKForge.Linux/Workspace/OrganizationTools.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.PlaceEncounter(Int32 species, Int32 form, Int32 cardIndex, Int32 targetBox, Int32 targetSlot)` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.PlaceLivingDex(Byte[] compressedBundle)` | — | Internal helper: reused LegalizerService.FillLivingDex calls this for the bundled dex. |
| `ISaveEngineSession.ReadEntity(Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/CreationTools.cs`, `src/PKForge.Linux/Workspace/ExchangeTools.cs`, `src/PKForge.Linux/Workspace/OrganizationTools.cs`, `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/ReferenceTools.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Application/AuditWorkflow.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.ReleaseSlot(Int32 box, Int32 slot)` | `src/PKForge.Linux/ParkLibrary.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Linux/Workspace/Transfers.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.RepairRTC()` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.RerollNatureKeepShiny(Int32 box, Int32 slot, Int32 nature)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.Serialize()` | `src/PKForge.Linux/Program.cs`, `src/PKForge.Linux/Native/SdlMusicPlayer.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/Transfers.cs`, `src/PKForge.Application/SaveCatalogue.cs`, `src/PKForge.Application/SaveOperations.cs`, `src/PKForge.Park/Platform.cs`, `src/PKForge.Park/SaveTransactions.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.SetAffixedRibbon(Int32 box, Int32 slot, Int32 ribbonIndex)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.SetCompassSetting(String id, Int32 choiceIndex)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.SetDexEntry(Int32 species, Boolean seen, Boolean caught)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.SetGrandUndergroundItemCount(Int32 itemId, Int32 count)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.SetItemCount(String pouchName, Int32 itemId, Int32 count)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.SetPokeBeanCount(Int32 index, Int32 count)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.SetPokerus(Int32 box, Int32 slot, PokerusStatus status)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.SetRibbon(Int32 box, Int32 slot, String id, Int32 value)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.SetStylePoints(Int32 value)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.SetTrainer(TrainerInfo trainer)` | `src/PKForge.Linux/Workspace/CreationTools.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.SetTrainerStats(TrainerStatsEdit edit)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.SortBoxes(SortCriteria criteria, IReadOnlyList boxes, Boolean reverse)` | `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.SwapBoxes(Int32 a, Int32 b)` | `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.UnlockAllLegalFashion()` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ISaveEngineSession.WithdrawDaycareToFirstEmptyBox(Int32 facility, Int32 slot)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `ILegalityService.Analyze(ISaveEngineSession session, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `ILegalityService.Sweep(ISaveEngineSession session, Action`2 onProgress, CancellationToken cancellationToken)` | `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Application/AuditWorkflow.cs` | Reference found; acceptance pending |
| `ILegalizerService.FillLivingDex(ISaveEngineSession session, Byte[] compressedBundle, Action`2 onProgress, CancellationToken cancellationToken)` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `ILegalizerService.FillSpecies(ISaveEngineSession session, IReadOnlyList species, Action`2 onProgress, CancellationToken cancellationToken)` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `ILegalizerService.Generate(ISaveEngineSession session, Int32 box, Int32 slot, GenerationRequest request)` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `ILegalizerService.GenerateData(ISaveEngineSession session, GenerationRequest request)` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `ILegalizerService.GenerateDataFromShowdown(ISaveEngineSession session, String showdownText, Boolean allowUnsupportedSpecies)` | `src/PKForge.Linux/Workspace/ExchangeTools.cs` | Reference found; acceptance pending |
| `ILegalizerService.GenerateEggs(ISaveEngineSession session, IReadOnlyList species, EggOptions options, Action`2 onProgress, CancellationToken cancellationToken)` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `ILegalizerService.GenerateFromShowdown(ISaveEngineSession session, Int32 box, Int32 slot, String showdownText, Boolean allowUnsupportedSpecies)` | — | Equivalent orchestration: ShowdownTeamService previews/generates/places selected sets through the legalizer. |
| `ILegalizerService.LegalizeSlot(ISaveEngineSession session, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ILegalizerService.LegalizeSlots(ISaveEngineSession session, IReadOnlyList slots, Action`2 onProgress, CancellationToken cancellationToken)` | `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `IEventDatabaseService.ExportCard(ISaveEngineSession session, Int32 giftId)` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `IEventDatabaseService.ExportEntity(ISaveEngineSession session, Int32 giftId)` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `IEventDatabaseService.GetGifts(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `IEventDatabaseService.GetSaveProfile(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `IEventDatabaseService.Receive(ISaveEngineSession session, Int32 giftId, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `IEventDatabaseService.ReceiveItems(ISaveEngineSession session, Int32 giftId)` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `IMonInfoService.DryRunBatch(ISaveEngineSession session, IReadOnlyList instructions, IReadOnlyList boxes, IReadOnlyList slots)` | `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `IMonInfoService.GetAbilityChoices(ISaveEngineSession session, Int32 species, Int32 form)` | `src/PKForge.Linux/Workspace/CreationTools.cs`, `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `IMonInfoService.GetHeldItems(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `IMonInfoService.GetHiddenPowerType(ISaveEngineSession session, IReadOnlyList ivs)` | `src/PKForge.Linux/Workspace/ReferenceTools.cs` | Reference found; acceptance pending |
| `IMonInfoService.GetIVsForHiddenPower(ISaveEngineSession session, IReadOnlyList ivs, Int32 type)` | `src/PKForge.Linux/Workspace/ReferenceTools.cs` | Reference found; acceptance pending |
| `IMonInfoService.GetLevelInfo(ISaveEngineSession session, Int32 box, Int32 slot, Int32 level, StatPreviewOverrides pending)` | `src/PKForge.Linux/Workspace/ReferenceTools.cs` | Reference found; acceptance pending |
| `IMonInfoService.GetMaxPPByUps(ISaveEngineSession session, Int32 box, Int32 slot, Int32 move)` | `src/PKForge.Linux/Workspace/ReferenceTools.cs` | Reference found; acceptance pending |
| `IMonInfoService.GetMove(ISaveEngineSession session, Int32 move)` | `src/PKForge.Linux/Workspace/ReferenceTools.cs` | Reference found; acceptance pending |
| `IMonInfoService.GetMoveChoices(ISaveEngineSession session, Int32 box, Int32 slot, Nullable species, Nullable form)` | `src/PKForge.Linux/Workspace/CreationTools.cs`, `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/ReferenceTools.cs` | Reference found; acceptance pending |
| `IMonInfoService.GetSpeciesCard(ISaveEngineSession session, Int32 species, Int32 form)` | `src/PKForge.Linux/Workspace/CollectionScreen.cs`, `src/PKForge.Linux/Workspace/ReferenceTools.cs` | Reference found; acceptance pending |
| `ILegalityAssistService.Apply(ISaveEngineSession session, LegalityFixPreview preview)` | `src/PKForge.Linux/Workspace/PksmTools.cs`, `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ILegalityAssistService.GetMoveLegality(ISaveEngineSession session, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/ReferenceTools.cs` | Reference found; acceptance pending |
| `ILegalityAssistService.GetReport(ISaveEngineSession session, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `ILegalityAssistService.Preview(ISaveEngineSession session, Int32 box, Int32 slot, LegalityFix fix)` | `src/PKForge.Linux/Workspace/ExchangeTools.cs`, `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Linux/Workspace/Transfers.cs` | Reference found; acceptance pending |
| `IStatPreviewService.PreviewSlot(ISaveEngineSession session, Int32 box, Int32 slot, StatPreviewOverrides overrides)` | `src/PKForge.Linux/Workspace/ReferenceTools.cs` | Reference found; acceptance pending |
| `IStatPreviewService.PreviewSpecies(ISaveEngineSession session, Int32 species, Int32 form, Int32 level)` | `src/PKForge.Linux/Workspace/CreationTools.cs` | Reference found; acceptance pending |
| `IEvolutionService.Evolve(ISaveEngineSession session, Int32 box, Int32 slot, EvolutionRequest request)` | `src/PKForge.Linux/Workspace/ExchangeTools.cs` | Reference found; acceptance pending |
| `IEvolutionService.Plan(ISaveEngineSession session, Int32 box, Int32 slot, Boolean hax)` | `src/PKForge.Linux/Workspace/Autopilot.cs`, `src/PKForge.Linux/Workspace/CreationTools.cs`, `src/PKForge.Linux/Workspace/ExchangeTools.cs`, `src/PKForge.Linux/Workspace/PksmTools.cs` | Reference found; acceptance pending |
| `IEncounterLookup.Describe(Int32 species, Int32 form)` | `src/PKForge.Linux/ParkLibrary.cs`, `src/PKForge.Linux/Workspace/Autopilot.cs`, `src/PKForge.Linux/Workspace/CollectionScreen.cs`, `src/PKForge.Linux/Workspace/CreationTools.cs`, `src/PKForge.Linux/Workspace/ExchangeTools.cs`, `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/ReferenceTools.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Linux/Workspace/SummaryScreen.cs`, `src/PKForge.Linux/Workspace/Transfers.cs` | Reference found; acceptance pending |
| `IMonSummaryService.Build(ISaveEngineSession session, Int32 box, Int32 slot, Boolean analyzeLegality)` | `src/PKForge.Linux/Workspace/Autopilot.cs`, `src/PKForge.Linux/Workspace/CollectionScreen.cs`, `src/PKForge.Linux/Workspace/SummaryScreen.cs` | Reference found; acceptance pending |
| `IBankService.Add(Byte[] data, BankEntryInfo info)` | `src/PKForge.Linux/ParkLibrary.cs`, `src/PKForge.Linux/Program.cs`, `src/PKForge.Linux/Native/InputRouter.cs`, `src/PKForge.Linux/Native/SdlMusicPlayer.cs`, `src/PKForge.Linux/Ui/CanvasView.cs`, `src/PKForge.Linux/Ui/Dispatcher.cs`, `src/PKForge.Linux/Ui/PadMenu.cs`, `src/PKForge.Linux/Ui/Views.cs`, `src/PKForge.Linux/Workspace/Autopilot.cs`, `src/PKForge.Linux/Workspace/CollectionScreen.cs`, `src/PKForge.Linux/Workspace/CreationTools.cs`, `src/PKForge.Linux/Workspace/ExchangeTools.cs`, `src/PKForge.Linux/Workspace/NativeDialogs.cs`, `src/PKForge.Linux/Workspace/NativeScreenStack.cs`, `src/PKForge.Linux/Workspace/OrganizationTools.cs`, `src/PKForge.Linux/Workspace/PksmTools.cs`, `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/ReferenceTools.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Linux/Workspace/SummaryScreen.cs`, `src/PKForge.Linux/Workspace/Transfers.cs`, `src/PKForge.Linux/Workspace/WorkspaceHost.cs`, `src/PKForge.Application/AuditWorkflow.cs`, `src/PKForge.Application/BankStaging.cs`, `src/PKForge.Application/CollectionWorkflow.cs`, `src/PKForge.Application/DocumentAliases.cs`, `src/PKForge.Application/JournaledBankService.cs`, `src/PKForge.Application/PksmWorkflow.cs`, `src/PKForge.Application/QrImportWorkflow.cs`, `src/PKForge.Application/RecoveryJournal.cs`, `src/PKForge.Application/SaveCatalogue.cs`, `src/PKForge.Application/SaveOperations.cs`, `src/PKForge.Park/SpritePrefetch.cs` | Reference found; acceptance pending |
| `IBankService.AddBox()` | `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Application/JournaledBankService.cs`, `src/PKForge.Application/SaveOperations.cs` | Reference found; acceptance pending |
| `IBankService.AddMany(IReadOnlyList deposits)` | `src/PKForge.Linux/Workspace/CreationTools.cs`, `src/PKForge.Linux/Workspace/ExchangeTools.cs`, `src/PKForge.Linux/Workspace/OrganizationTools.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Linux/Workspace/Transfers.cs`, `src/PKForge.Application/JournaledBankService.cs`, `src/PKForge.Application/SaveOperations.cs` | Reference found; acceptance pending |
| `IBankService.CompleteMigration(Int32 version, IReadOnlyList updates)` | `src/PKForge.Application/JournaledBankService.cs`, `src/PKForge.Application/SaveOperations.cs` | Reference found; acceptance pending |
| `IBankService.GetAll()` | `src/PKForge.Linux/ParkLibrary.cs`, `src/PKForge.Linux/Program.cs`, `src/PKForge.Linux/Workspace/Autopilot.cs`, `src/PKForge.Linux/Workspace/CreationTools.cs`, `src/PKForge.Linux/Workspace/OrganizationTools.cs`, `src/PKForge.Linux/Workspace/PksmTools.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Linux/Workspace/Transfers.cs`, `src/PKForge.Application/CollectionWorkflow.cs`, `src/PKForge.Application/JournaledBankService.cs`, `src/PKForge.Application/PokemonWorkflow.cs`, `src/PKForge.Application/SaveOperations.cs` | Reference found; acceptance pending |
| `IBankService.GetData(Guid id)` | `src/PKForge.Linux/ParkLibrary.cs`, `src/PKForge.Linux/Program.cs`, `src/PKForge.Linux/Workspace/OrganizationTools.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Linux/Workspace/Transfers.cs`, `src/PKForge.Application/JournaledBankService.cs`, `src/PKForge.Application/PokemonWorkflow.cs`, `src/PKForge.Application/SaveOperations.cs` | Reference found; acceptance pending |
| `IBankService.Move(Guid id, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Application/JournaledBankService.cs`, `src/PKForge.Application/LocalFileSystem.cs`, `src/PKForge.Application/RecoveryJournal.cs`, `src/PKForge.Application/SaveCatalogue.cs`, `src/PKForge.Application/SaveOperations.cs`, `src/PKForge.Park/Platform.cs`, `src/PKForge.Park/SaveTransactions.cs` | Reference found; acceptance pending |
| `IBankService.Place(IReadOnlyList placements)` | `src/PKForge.Linux/Workspace/ExchangeTools.cs`, `src/PKForge.Linux/Workspace/OrganizationTools.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Application/JournaledBankService.cs`, `src/PKForge.Application/SaveOperations.cs` | Reference found; acceptance pending |
| `IBankService.RemapBoxes(BankBoxRemap remap)` | `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Application/JournaledBankService.cs`, `src/PKForge.Application/SaveOperations.cs` | Reference found; acceptance pending |
| `IBankService.Remove(Guid id)` | `src/PKForge.Linux/ParkLibrary.cs`, `src/PKForge.Linux/Native/InputRouter.cs`, `src/PKForge.Linux/Ui/CanvasView.cs`, `src/PKForge.Linux/Ui/PadMenu.cs`, `src/PKForge.Linux/Ui/Views.cs`, `src/PKForge.Linux/Workspace/NativeDialogs.cs`, `src/PKForge.Linux/Workspace/NativeScreenStack.cs`, `src/PKForge.Linux/Workspace/OrganizationTools.cs`, `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Application/JournaledBankService.cs`, `src/PKForge.Application/SaveCatalogue.cs`, `src/PKForge.Application/SaveOperations.cs`, `src/PKForge.Park/Platform.cs` | Reference found; acceptance pending |
| `IBankService.RemoveMany(IReadOnlyList ids)` | `src/PKForge.Linux/Workspace/Transfers.cs`, `src/PKForge.Application/JournaledBankService.cs`, `src/PKForge.Application/SaveOperations.cs` | Reference found; acceptance pending |
| `IBankService.Replace(Guid id, Byte[] data, BankEntryInfo info)` | `src/PKForge.Linux/Native/InputRouter.cs`, `src/PKForge.Linux/Workspace/NativeForms.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Application/JournaledBankService.cs`, `src/PKForge.Application/PokemonWorkflow.cs`, `src/PKForge.Application/SaveOperations.cs`, `src/PKForge.Park/Platform.cs` | Reference found; acceptance pending |
| `IBankService.UpdateInfo(IReadOnlyList updates)` | `src/PKForge.Application/JournaledBankService.cs`, `src/PKForge.Application/SaveOperations.cs` | Reference found; acceptance pending |
| `ClockRepairService.ApplyBerryFix(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `ClockRepairService.ArmInGameReset(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `ClockRepairService.GetState(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `ClockRepairService.IsSupported(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs` | Reference found; acceptance pending |
| `ClockRepairService.ResetClocks(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `ClockRepairService.SetClocks(ISaveEngineSession session, Gen3Clock initial, Gen3Clock elapsed)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `RoamerService.GetRoamers(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `RoamerService.IsSupported(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs` | Reference found; acceptance pending |
| `RoamerService.Reroll(ISaveEngineSession session, Int32 index, Boolean shiny)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `RoamerService.SetGen6State(ISaveEngineSession session, Int32 state, Nullable timesEncountered)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `EventResetService.GetEvents(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `EventResetService.IsSupported(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs` | Reference found; acceptance pending |
| `EventResetService.Reset(ISaveEngineSession session, String id)` | `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Application/DocumentAliases.cs` | Reference found; acceptance pending |
| `EntralinkService.ClearForest(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `EntralinkService.FillForestLegally(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `EntralinkService.GetPassPowerChoices()` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `EntralinkService.GetState(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `EntralinkService.IsSupported(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs` | Reference found; acceptance pending |
| `EntralinkService.PassPowerName(Byte value)` | — | Internal helper: GetState returns named pass powers; native SetPassPower uses GetPassPowerChoices. |
| `EntralinkService.SetLevels(ISaveEngineSession session, Int32 whiteForest, Int32 blackCity)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `EntralinkService.SetPassPower(ISaveEngineSession session, Int32 slot, Int32 power)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `EntralinkService.UnlockAllAreas(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `EntralinkService.UnlockAllMissions(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `ApricornService.GetApricorns(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `ApricornService.IsSupported(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs` | Reference found; acceptance pending |
| `ApricornService.SetAll(ISaveEngineSession session, Int32 count)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `ApricornService.SetCount(ISaveEngineSession session, Int32 index, Int32 count)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `PokewalkerService.GetEncounters(Int32 course)` | — | Internal helper: GetState returns every course with its encounters for the report explorer. |
| `PokewalkerService.GetState(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `PokewalkerService.IsSupported(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs` | Reference found; acceptance pending |
| `PokewalkerService.SetCounters(ISaveEngineSession session, UInt32 steps, UInt32 watts)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `PokewalkerService.SetCourse(ISaveEngineSession session, Int32 course, Boolean unlocked)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `PokewalkerService.UnlockAll(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `HoneyTreeService.GetGroupSpecies(ISaveEngineSession session, Int32 group)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `HoneyTreeService.GetMunchlaxTrees(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `HoneyTreeService.GetMunchlaxTrees(UInt32 id32)` | — | Internal overload: the session overload, exposed in world tools, calls the trainer-ID overload. |
| `HoneyTreeService.GetTrees(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `HoneyTreeService.IsSupported(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs` | Reference found; acceptance pending |
| `HoneyTreeService.SetMunchlaxReady(ISaveEngineSession session, Int32 index)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `HoneyTreeService.SetTree(ISaveEngineSession session, Int32 index, UInt32 time, Int32 group, Int32 slot, Int32 shakes)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `HoneyTreeService.Slather(ISaveEngineSession session, Int32 index)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `KeyItemEventService.Disable(ISaveEngineSession session, String id)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `KeyItemEventService.Enable(ISaveEngineSession session, String id)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `KeyItemEventService.GetEvents(ISaveEngineSession session)` | `SaveTools.EngineToolAsync` | Explicit world-service form; acceptance pending |
| `KeyItemEventService.IsSupported(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs` | Reference found; acceptance pending |
| `MonFieldService.ApplyTrainerEdit(ISaveEngineSession session, Int32 box, Int32 slot, TrainerFieldsEdit edit)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.ClearTechRecords(ISaveEngineSession session, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.Describe(ISaveEngineSession session, Int32 box, Int32 slot)` | `src/PKForge.Linux/ParkLibrary.cs`, `src/PKForge.Linux/Workspace/Autopilot.cs`, `src/PKForge.Linux/Workspace/CollectionScreen.cs`, `src/PKForge.Linux/Workspace/CreationTools.cs`, `src/PKForge.Linux/Workspace/ExchangeTools.cs`, `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/ReferenceTools.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/StorageTools.cs`, `src/PKForge.Linux/Workspace/SummaryScreen.cs`, `src/PKForge.Linux/Workspace/Transfers.cs` | Reference found; acceptance pending |
| `MonFieldService.GetForm(ISaveEngineSession session, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.GetFormArgument(ISaveEngineSession session, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.GetHandlerLanguageChoices(ISaveEngineSession session, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.GetMemory(ISaveEngineSession session, Int32 box, Int32 slot, Boolean handler)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.GetMemoryOptions(ISaveEngineSession session, Int32 box, Int32 slot, Boolean handler, Int32 memory)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.GetShiny(ISaveEngineSession session, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.GetStatNature(ISaveEngineSession session, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.GetTechRecords(ISaveEngineSession session, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.GetTrainers(ISaveEngineSession session, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.IsSupported(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs` | Reference found; acceptance pending |
| `MonFieldService.PreviewStatNature(ISaveEngineSession session, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.SetAllLegalTechRecords(ISaveEngineSession session, Int32 box, Int32 slot)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.SetEncryptionConstant(ISaveEngineSession session, Int32 box, Int32 slot, UInt32 ec)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.SetForm(ISaveEngineSession session, Int32 box, Int32 slot, Int32 form)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.SetFormArgument(ISaveEngineSession session, Int32 box, Int32 slot, UInt32 value)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.SetMemory(ISaveEngineSession session, Int32 box, Int32 slot, MemoryEdit edit)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.SetPid(ISaveEngineSession session, Int32 box, Int32 slot, UInt32 pid)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.SetShinyKind(ISaveEngineSession session, Int32 box, Int32 slot, ShinyKind kind)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.SetStatNature(ISaveEngineSession session, Int32 box, Int32 slot, Int32 nature)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.SetTechRecord(ISaveEngineSession session, Int32 box, Int32 slot, Int32 index, Boolean learned)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs` | Reference found; acceptance pending |
| `MonFieldService.TryParseHex(String text, UInt32& value)` | — | Equivalent: native numeric input accepts decimal/0x hex and checks the typed range. |
| `EventFlagService.Categories(IEnumerable entries, Boolean showUnlabeled)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `EventFlagService.Filter(IEnumerable entries, EventFilter filter)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `EventFlagService.IsSupported(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs` | Reference found; acceptance pending |
| `EventFlagService.Load(ISaveEngineSession session)` | `src/PKForge.Linux/Native/InputRouter.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `EventFlagService.SetFlag(ISaveEngineSession session, EventFlagBank bank, Int32 index, Boolean value)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `EventFlagService.SetWork(ISaveEngineSession session, Int32 index, Int64 value)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `EventFlagService.UnsupportedReason(ISaveEngineSession session)` | — | Capability helper: IsSupported uses this; unsupported event editors are unavailable. |
| `SaveBlockEditorService.GetBlocks(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `SaveBlockEditorService.IsSupported(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs` | Reference found; acceptance pending |
| `SaveBlockEditorService.SetBool(ISaveEngineSession session, UInt32 key, Boolean value)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `SaveBlockEditorService.SetNumber(ISaveEngineSession session, UInt32 key, String text)` | `src/PKForge.Linux/Workspace/SaveTools.cs` | Reference found; acceptance pending |
| `BoxLayoutService.GetBox(ISaveEngineSession session, Int32 box)` | `src/PKForge.Linux/Workspace/StorageScreen.cs` | Reference found; acceptance pending |
| `BoxLayoutService.GetNameMaxLength(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `BoxLayoutService.GetWallpaperResourceName(GameVersion version, Int32 index)` | — | Internal helper: GetWallpapers/GetBox supply the native wallpaper asset names. |
| `BoxLayoutService.GetWallpapers(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `BoxLayoutService.IsSupported(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/PokemonEditor.cs`, `src/PKForge.Linux/Workspace/SaveTools.cs`, `src/PKForge.Linux/Workspace/StorageScreen.cs` | Reference found; acceptance pending |
| `BoxLayoutService.Rename(ISaveEngineSession session, Int32 box, String name)` | `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `BoxLayoutService.SetWallpaper(ISaveEngineSession session, Int32 box, Int32 wallpaper)` | `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `BoxLayoutService.SupportsNames(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `BoxLayoutService.SupportsWallpapers(ISaveEngineSession session)` | `src/PKForge.Linux/Workspace/StorageTools.cs` | Reference found; acceptance pending |
| `BulkBoxActions.FindCloneExtras(ISaveEngineSession session, IReadOnlyList slots)` | `src/PKForge.Linux/Workspace/ReferenceTools.cs` | Reference found; acceptance pending |
| `BulkBoxActions.Heal(ISaveEngineSession session, IReadOnlyList slots)` | `src/PKForge.Linux/Workspace/ReferenceTools.cs` | Reference found; acceptance pending |
| `BulkBoxActions.SetCurrentTrainerAsHandler(ISaveEngineSession session, IReadOnlyList slots)` | `src/PKForge.Linux/Workspace/ReferenceTools.cs` | Reference found; acceptance pending |
| `BulkBoxActions.SetShiny(ISaveEngineSession session, IReadOnlyList slots, Boolean shiny, Boolean keepLegal, Action`2 onProgress, CancellationToken cancellationToken)` | `src/PKForge.Linux/Workspace/ReferenceTools.cs` | Reference found; acceptance pending |

223 method overloads; 0 without a direct source reference or the explicit world-service form.

Review includes helpers that may be covered by a higher-level operation. No row is automatically declared implemented or tested by this script.
