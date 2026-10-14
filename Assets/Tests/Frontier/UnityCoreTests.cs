#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace Starfall.Tests
{
    public sealed class UnityCoreTests
    {
        [Test] public void HitStopConsumesExactImpactTime() { CoreChecks.ShortHitStopReleasesUnusedFrameTime(Data()); }
        [Test] public void DeadPlayersCannotEnterDelayedFinale() { CoreChecks.BossFinaleCannotInterruptDeathRecovery(Data()); }
        [Test] public void SaveSwitchesResetTransientInputState() { CoreChecks.SwitchingSaveSlotsDoesNotInheritOldAimOrFishingState(Data()); }
        [Test] public void PiercingProjectilesPreserveSweptContactOrder() { CoreChecks.PiercingVolleysKeepEveryContactWithReusableTargetSnapshot(Data()); }
        [Test] public void ZeroDamageDoesNotCauseHitStunOrIFrames() { CoreChecks.NonDamagingContactsCannotGrantFreeHitImmunity(Data()); }
        [Test] public void SameCompanionSelectionDoesNotCancelHealing() { CoreChecks.ReselectingSameCompanionPreservesInFlightCommand(Data()); }
        [Test] public void InvalidWeaponDamageCannotHealEnemies() { CoreChecks.InvalidOffensiveDamageNeverHealsOrCreatesPhantomHits(Data()); }
        [Test] public void SwappedWeaponsCannotCarrySwordComboProgress() { CoreChecks.RealWeaponChangesRestartWeaponComboButReselectDoesNot(Data()); }
        [Test] public void PracticeExitNeverLeaksRedCirclesOrCounterBuffs() { CoreChecks.PracticeExitRetiresDummyHazardsAndEarnedCounter(Data()); }
        [Test] public void DrownedRuinsArchiveChoicesPersist() { CoreChecks.DrownedFloodArchiveRequiresRealTidesAndChoice(Data()); }
        [Test] public void RestoredRootguardHasRealEnemyInterrupts() { CoreChecks.RestoredRootGuardInterruptsOnlyNearbyThreats(Data()); }
        [Test] public void RootsongRequiresActualTimedForestTones() { CoreChecks.ThornwoodRootsongUsesPhysicalTimingAndPersistentHarmony(Data()); }
        [Test] public void StyleChangesRestartAttackChains() { CoreChecks.StyleChangesRestartCombosButRepeatedStylesPreserveAttacks(Data()); }
        [Test] public void LateAttackBufferHonorsStyleAndWeaponChanges() { CoreChecks.LateBasicAttacksBufferOnceAndRespectCombatProfiles(Data()); }
        [Test] public void RangedAttackChainsPayOffWithRealProjectileEffects() { CoreChecks.RangedThirdHitsProduceDistinctRealProjectiles(Data()); }
        [Test] public void CompanionSelfTargetFallsBackToFacing() { CoreChecks.ArcherCompanionCannotWasteVolleyOnItsOwnFootTarget(Data()); }
        [Test] public void FullInventoryDoesNotChargeForOverflowStock() { CoreChecks.RepeatablePurchasesCannotConsumeResourcesPastSavedStackCaps(Data()); }
        [Test] public void FieldCampsRespectElapsedCombatTimeAndActiveMissions() { CoreChecks.FieldCampRestAdvancesCombatTimeAndCannotSkipLiveCivilianWork(Data()); }
        [Test] public void FieldCampMenuReflectsSafetyAndWaystationUnlock() { CoreChecks.FieldCampMenuShowsRestOnlyWhenActuallyAvailable(Data()); }
        [Test] public void ActiveRelayCapturesCannotRestThroughTimedWaves() { CoreChecks.LiveGlasswindTrialCannotSkipTheFieldRestClock(Data()); }
        [Test] public void SameWeaponEquippingKeepsCombatIntent() { CoreChecks.EquippingIdenticalWeaponCannotEraseActiveAttack(Data()); }
        [Test] public void ReleaseEventGovernsContactPoses(){CoreChecks.ReleasedContactsSelectVisibleActivePoses(Data());}
        [Test] public void RecoveryWalkingRestoresEquipment(){CoreChecks.WalkingSkillRecoveryRestoresEquippedAppearance(Data());}
        [Test] public void RecoveryReactionEquipmentIdentity(){CoreChecks.RecoveryReactionsRetainDisplayedEquipment(Data());}
        [Test] public void ScoutPresenceAndSavedInteractions(){CoreChecks.ScoutPresenceMatchesInteractionAndSavedJourney(Data());}
        [Test] public void HeldAttackPreservesQContact(){CoreChecks.HeldAttacksPreserveSkillContactPoses(Data());}
        [Test] public void BufferedQRetainsInputAndJoinsRecovery(){CoreChecks.BufferedSkillsRetainIntentAndJoinRecovery(Data());}
        [Test] public void BufferedQLifetimeAndResources(){CoreChecks.BufferedSkillsHaveBoundedLifetimeAndResourceChecks(Data());}
        [Test] public void BufferedQVariableFrameDeadlines(){CoreChecks.BufferedSkillsRespectVariableFrameDeadlines(Data());}
        [Test] public void BufferedQWorldAndProfileBoundaries(){CoreChecks.BufferedSkillsClearAtWorldAndProfileBoundaries(Data());}
        [Test] public void PreparedQCombatValues(){CoreChecks.SkillsRetainPreparedCombatValues(Data());}
        [Test] public void PersistentFrostIdentity(){CoreChecks.FrostRetainsCasterProfileAndSource(Data());}
        [Test] public void FrostScheduledContacts(){CoreChecks.FrostPulseClockIsFrameRateIndependent(Data());}
        [Test] public void PlayerLessonsRequirePlayerHits(){CoreChecks.CompanionDamageDoesNotCompletePlayerLessons(Data());}
        [Test] public void DeathCannotStartOrDescendExpedition(){CoreChecks.RecoveryGuardsExpeditionTransitions(Data());}
        [Test] public void MalformedWorldPatrolSnapshotsAreSanitized() { CoreChecks.CorruptEnemySnapshotsCannotInjectOrHideValidPatrols(Data()); }
        [Test] public void ReadSaveIgnoresPreviousOpening(){CoreChecks.ReusedLoadsMatchFreshWorldState(Data());}
        [Test] public void MenuActionProfileCancelsBeforeMovement(){CoreChecks.ChangedActionProfilesCancelBeforeMovement(Data());}
        [Test] public void FlyingWeaponIdentity(){CoreChecks.FlyingShotsRetainReleaseProfile(Data());}
        [Test] public void LastPartialProjectileContact(){CoreChecks.ProjectileLastPartialFrameStillCollides(Data());}
        [Test] public void FrozenProjectileLifetime(){CoreChecks.ZeroDurationTicksPreserveLiveFlights(Data());}
        [Test] public void PracticeCannotInterruptRecovery(){CoreChecks.DeathCannotEnterPractice(Data());}
        [Test] public void MixedWorldStateStress(){CoreChecks.MixedStateTransitionsMaintainWorldInvariants(Data());}
        [Test] public void EncounterSpawnFootClearance(){CoreChecks.EveryEncounterSpawnHasValidFooting(Data());}
        [Test] public void TideGeometryBeforeContact(){CoreChecks.TideTransitionPrecedesAllAttackContacts(Data());}
        [Test] public void RegionalHazardRelease(){CoreChecks.RegionalHazardsKeepActualFootprint(Data());}
        [Test] public void DeathTransactionStopsTheFrame(){CoreChecks.LethalContactStopsRemainingSimulation(Data());}
        [Test] public void PracticePresentationTeardown(){CoreChecks.PracticeExitCancelsAllPendingPresentation(Data());}
        [Test] public void PursuitWorkspaceAndOwnedHudRoutes(){CoreChecks.SharedPathWorkspaceKeepsOwnedRoutes(Data());}
        [Test] public void JournalRefreshTriggers(){CoreChecks.JournalRecordsRefreshOnlyOnStateChanges(Data());}
        [Test] public void EnemyRecoveryAndArmoredFlanks(){CoreChecks.TimedRecoveryAndArmoredFlanksCreateCombatOpenings(Data());}
        [Test] public void LastDodgeFrameFacing(){CoreChecks.DashExitKeepsVisibleFacing(Data());}
        [Test] public void RegionalBossLaneSweeps(){CoreChecks.RegionalBossesSweepAlternatingLanes(Data());}
        [Test] public void FrostPrelateSafeBellExit(){CoreChecks.FrostPrelateRotatesSafeBellExit(Data());}
        [Test] public void RootReaperRootPincer(){CoreChecks.RootReaperClosesRootPincers(Data());}
        [Test] public void DodgeRunLegCadence(){CoreChecks.DashToRunRestartsLegCadence(Data());}
        [Test] public void DodgeEscapesAwardRiposte(){CoreChecks.EscapingTelegraphedAttacksEarnsOneCounter(Data());}
        [Test] public void DashDistanceAcrossFrameSizes(){CoreChecks.DashDistanceIsFrameStepIndependent(Data());}
        [Test] public void RecoveryAndFallenIdentity(){CoreChecks.StandingRecoveryAndCrossEquipmentDeath(Data());}
        [Test] public void EnemyFacingAndRetreat(){CoreChecks.StableEnemyFacingAndBackpedaling(Data());}
        [Test] public void ReactionFacing(){CoreChecks.RecoilRetainsInterruptedBodyDirection(Data());}
        [Test] public void MeleeWarningAndContact(){CoreChecks.MeleeWarningGeometryMatchesActualContact(Data());}
        [Test] public void MutationSafeCombat(){CoreChecks.CombatSnapshotsSurviveDeathsAndReinforcements(Data());}
        [Test] public void CapturedTargetAndExactMuzzle(){CoreChecks.CapturedTargetKeepsBodyAndMuzzleTogether(Data());}
        [Test] public void ThirtySecondDistanceDrivenGaits(){CoreChecks.DistanceDrivenThirtySecondWalking(Data());}
        [Test] public void SharedAttackMobilityAndDirections(){CoreChecks.UnifiedMobilityAndLockedDirections(Data());}
        [Test] public void AllArenaRoutesAndOpeningCollision(){CoreChecks.ArenaBoundariesAndOpeningTransactions(Data());}
        [Test] public void PauseHitStopAndEffectFinishing(){CoreChecks.SharedPauseAndEffectFinishingClock(Data());}
        [Test] public void ActualCompanionAndEnemyTravel(){CoreChecks.AlliesAndEnemiesUseActualTravel(Data());}
        [Test] public void QuietMaterialFootContacts(){CoreChecks.SoftMaterialFootContacts(Data());}
        [Test] public void MuzzleWallsAndCrossEquipmentTechniques(){CoreChecks.PointBlankWallsAndCrossEquipmentActions(Data());}
        [Test] public void SealedCacheRoutesAndCollision(){CoreChecks.ClosedCacheRoutesAndCollision(Data());}
        [Test] public void ActualWeaponTipsAndLaunchHeight(){CoreChecks.AuthoredMuzzlesAndContinuousLaunchHeight(Data());}
        [Test] public void InnDoorFurnitureRecoveryAndSaves() { CoreChecks.InnRoomAndCollisionPersistence(Data()); }
        [Test] public void ActualContactFramesAndCancellation() { CoreChecks.ContactFramesAndCancellation(Data()); }
        [Test] public void FlightAndEnemyStrikeFrames() { CoreChecks.ProjectilesAndEnemyContact(Data()); }
        [Test] public void DeathPosesAndSafeResume() { CoreChecks.DeathPresentationAndResume(Data()); }
        [Test] public void TutorialSaveMigrationAndPagedDialogue() { CoreChecks.TutorialAndPresentationMigration(Data()); }
        [Test] public void RoadsideTravelersHaveTwoRescueRoutes() { CoreChecks.MissingTravelersOfferSupplyAndCombatRescues(Data()); }
        [Test] public void EscortPlayersCanPauseAndTendSurvivors() { CoreChecks.RescueCivilianWaitFollowAndOneUseAidArePlayable(Data()); }
        [Test] public void RescueCiviliansWalkInsteadOfSliding() { CoreChecks.RescuedSurvivorsUseActualDistanceDrivenMovement(Data()); }
        [Test] public void CivilianRescuesUseRealRegionalWeather() { CoreChecks.EvacueesUseRealClimateSheltersAndShade(Data()); }
        [Test] public void RescueCiviliansAppearAndWarningsRetire() { CoreChecks.RescueCivilianAppearsOnSiteAndEncounterEffectsRetire(Data()); }
        [Test] public void CivilianArchersKeepActualFiringDistance() { CoreChecks.CivilianRescueArcherUsesReadableRealStandoff(Data()); }
        [Test] public void RescueSafetyWarningsAndDifficultyScaling() { CoreChecks.RescueThreatDifficultyAndOneShotWarnings(Data()); }
        [Test] public void RescueArrowsRequirePhysicalFlightAndInterception() { CoreChecks.RescueArrowsUseActualFlightAndCanBeBodyBlocked(Data()); }
        [Test] public void RescueRallyUsesVisibleCivilianNotMapProp() { CoreChecks.FieldRallyInteractionTargetsRealCivilianFoot(Data()); }
        [Test] public void EmergencyRescueRallyIsTacticalAndFinite() { CoreChecks.EmergencyCivilianRallyUsesLiveEnergyAndOncePerWave(Data()); }
        [Test] public void CivilianStrikeTimingAndBodyguardCollision() { CoreChecks.CivilianDefenseStrikesMatchReleaseAndBodyguardPosition(Data()); }
        [Test] public void RescueRaiderAITargetsActualCivilians() { CoreChecks.RescueRaiderAIActuallyProtectsCivilians(Data()); }
        [Test] public void CiviliansTraverseEntireReturnPaths() { CoreChecks.ReliefSurvivorsPhysicallyCompleteAllRegionalReturnRoutes(Data()); }
        [Test] public void RescueWeatherCanopyPreventsRealClimateDamage() { CoreChecks.EscortCanopyProtectsFromRealClimateAndStaysSpent(Data()); }
        [Test] public void RescuePortalsNeverAbandonLiveEscort() { CoreChecks.ActiveCivilianRescuesCannotBeLostThroughPortals(Data()); }
        [Test] public void MistportEscortResumeHandlesHighTide() { CoreChecks.MistportEscortCheckpointRecoversOnlyFloodedFooting(Data()); }
        [Test] public void EscortHurryCreatesRealSpeedAndSurvivesSave() { CoreChecks.CivilianEscortHurryAddsRealSpeedAndCannotFarmReload(Data()); }
        [Test] public void ReturnEscortResumeAfterSaving() { CoreChecks.CivilianEscortReloadContinuesRealReturn(Data()); }
        [Test] public void CorruptedEscortCheckpointsCannotStartMissions() { CoreChecks.OldAndCorruptCivilianCheckpointsRemainSafe(Data()); }
        [Test] public void EscortSurvivorBracesForCommittedThreat() { CoreChecks.CivilianBracesForRealEscortWindupThenResumesWalking(Data()); }
        [Test] public void EscortFormationIsTacticalAndOncePerReturn() { CoreChecks.EscortGuardUsesRealEnergyAndInterceptsPatrolDamage(Data()); }
        [Test] public void EscortPatrolArrowsActuallyFlyAndReset() { CoreChecks.EscortPatrolArrowsRequireRealFlightAndRetireOnFailure(Data()); }
        [Test] public void FarshoreSurvivorsRequireDefense() { CoreChecks.RescueSurvivorsRequireRealProtectionAndSafeTravel(Data()); }
        [Test] public void FarshoreEvacuationRequiresRealTwoWaveCombat() { CoreChecks.FarshoreEvacuationHasRealCombatAndPersistentFirstClear(Data()); }
        [Test] public void CiviliansCanRecoverAtRepairedRefuges() { CoreChecks.CivilianReliefActuallyHealsAndProtectsOuterClimate(Data()); }
        [Test] public void FarshoreInnReliefRoutesRemainSafe() { CoreChecks.FarshoreReliefRoutesLinkExistingInnAndStations(Data()); }
        [Test] public void FarshoreCivilianReliefBuildsRealShelters() { CoreChecks.FarshoreReliefWorksProvidePersistentClimateSanctuaries(Data()); }
        [Test] public void FarshoreFieldSurveys() { CoreChecks.FarshoreStationSurveysRequireRealOutAndBackTravel(Data()); }
        [Test] public void FarshoreStationTravelIsSafe() { CoreChecks.FarshoreStationNetworkProvidesSafeUnlockAwareTravel(Data()); }
        [Test] public void RestoredEnemyTimersRemainPlayable() { CoreChecks.BoundedEnemyOpeningDelaysAndRestoredPatrols(Data()); }
        [Test] public void EchoHazardsRetireOnFinishingHit() { CoreChecks.EliteVictoryRetiresWarningsBeforeNextDamageStep(Data()); }
        [Test] public void SpecialEncountersCannotOverlap() { CoreChecks.OptionalEncountersNeverStartInsideEachOther(Data()); }
        [Test] public void WorldLedgerAndEchoLoreStayCoherent() { CoreChecks.LivingWorldLedgerAndAtlasEchoShareOneStory(Data()); }
        [Test] public void ChroniclePageNavigationRespectsMilestones() { CoreChecks.ChronicleChapterNavigationNeverLeaksFutureEvents(Data()); }
        [Test] public void ChronicleReflectsActualRegionalDecisions() { CoreChecks.ChronicleRetellsRegionalChoicesAndSurvivesReload(Data()); }
        [Test] public void WorldChronicleRespectsPlayerDiscovery() { CoreChecks.WorldChronicleRespectsChapterProgressAndRealInteractions(Data()); }
        [Test] public void CampaignAndCouncilEpiloguesMatchWorldLore() { CoreChecks.IndependentEndingsAndCampaignLoreRemainConsistent(Data()); }
        [Test] public void SurveyorEchoHasLiveTimedCombat() { CoreChecks.SurveyorEchoCombatHasWarningsAndSafeRewards(Data()); }
        [Test] public void CounterpointTrialsCanBeSafelyAbandoned() { CoreChecks.StarrootTrialsSupportRealSafeWithdrawal(Data()); }
        [Test] public void CounterpointCombatPulsesAreTelegraphedAndDodgeable() { CoreChecks.StarrootCombatPulsesWarnAndCanActuallyBeDodged(Data()); }
        [Test] public void NewCounterpointChronicleTracksActualFindings() { CoreChecks.NewCounterpointChronicleRevealsOnlyRecoveredMusic(Data()); }
        [Test] public void SealedStarrootNodesProvideSafeLocalRest() { CoreChecks.CalibratedStarrootSitesOfferSafePersistentRest(Data()); }
        [Test] public void CitadelCommonsCitizensReceivePermanentEnergyNetwork() { CoreChecks.CitadelCommonsPermanentEnergyNetworkPersistsWithoutNewSaveFields(Data()); }
        [Test] public void CitadelCommonsTimedSecurityExpiresSafely() { CoreChecks.CitadelCommonsTimedSecurityCannotGrantFreeCompletion(Data()); }
        [Test] public void CitadelCommonsSentinelsNeedRealPlayerCombat() { CoreChecks.CitadelCommonsSentinelsRequireRealPlayerCombatAndCannotFarm(Data()); }
        [Test] public void CitadelCommonsSecurityUsesDodgeableSurges() { CoreChecks.CitadelCommonsFloorSurgesWarnDodgeAndCleanUp(Data()); }
        [Test] public void CitadelCommonsStoryRevealsOnlyTrueEvidence() { CoreChecks.CitadelCommonsChronicleRevealsTrueProgress(Data()); }
        [Test] public void AfterglowExpeditionUnlocksPermanentHighlandRefuges() { CoreChecks.AfterglowExpeditionHasRealFieldVisitsRewardsAndWarming(Data()); }
        [Test] public void SkywatchPrismGroundingIsPlayableAndFinite() { CoreChecks.FrostSkywatchPrismGroundingCreatesRealOneUseBossOpening(Data()); }
        [Test] public void FrostSkywatchChronicleRevealsTrueObservations() { CoreChecks.FrostSkywatchChronicleOnlyRevealsObservedEvidence(Data()); }
        [Test] public void FrostSkywatchIsFullNewHighlandStory() { CoreChecks.FrostSkywatchIsPlayableOrderedMysteryWithRealBoss(Data()); }
        [Test] public void FrostSkywatchHasDodgeableWarningsAndSafeRetries() { CoreChecks.FrostSkywatchHazardsAreDodgeableAndWithdrawalIsSafe(Data()); }
        [Test] public void CitadelCommonsStagesAndPublicRelease() { CoreChecks.CitadelCommonsThreeStageCombatAndPublicKey(Data()); }
        [Test] public void CitadelCommonsRequiresMainGuardianAndSupportsWithdrawal() { CoreChecks.CitadelCommonsStoryLocksAndWithdrawal(Data()); }
        [Test] public void StarrootCounterpointHasNewRegionsAndActualFights() { CoreChecks.StarrootCounterpointIsNewPlayableCrossRegionStory(Data()); }
        [Test] public void LostSurveyorAtlasIsPlayable() { CoreChecks.LostSurveyorAtlasHasRealSequenceAndOneTimeRewards(Data()); }
        [Test] public void FarshoreWeatherStationsAreLive() { CoreChecks.FarshoreWeatherStationsChangeActualClimateAndTide(Data()); }
        [Test] public void RoadwardenSignalChartAndSave() { CoreChecks.RoadwardenSignalsUnlockOnePersistentTravelReward(Data()); }
        [Test] public void LiveQuestProgressAndTracking() { CoreChecks.QuestTrackingTransitions(Data()); }
        [Test] public void QuestRoutesRespectCollisionAndTides() { CoreChecks.NavigationRespectsWorldCollision(Data()); }
        [Test] public void SafePracticeAndReplay() { CoreChecks.InteractivePracticeAndReplay(Data()); }
        [Test] public void CorePowersOpenSecretRoutes() { CoreChecks.ExplorationPowersAndDiscoveries(Data()); }
        [Test] public void WeaponStylesAndEnemyCounters() { CoreChecks.CombatStylesAndEnemyCounters(Data()); }
        [Test] public void EncounterSnapshotsAndWorldResponse() { CoreChecks.WorldSnapshotsAndChoiceResponses(Data()); }
        [Test] public void MenuFocusFollowsGeometry() { CoreChecks.PredictableMenuFocus(Data()); }
        [Test] public void OriginalScoresAndFeedback() { CoreChecks.SoundFeedbackAndRegionalScores(Data()); }
        [Test] public void AreaWarningsBlockTravelAndInnRest() { CoreChecks.LiveAreaWarningsBlockVoluntaryEscapeAndRest(Data()); }
        [Test] public void SafeTravelAndCivicHudKeepExactCombatDistances() { CoreChecks.TravelClearanceAndCivicEnemyHudUseExactRealDistances(Data()); }
        [Test] public void TimedCivilianFieldMissionsAreMutuallyExclusive() { CoreChecks.SimultaneousTimedCivilianTasksCannotHideEachOthersObjectives(Data()); }
        [Test] public void FieldMissionsDoNotSkipTheirClockAtInn() { CoreChecks.ActiveFieldMissionsCannotFreeRestOrSkipTheirTimedRuns(Data()); }
        [Test] public void HostilesAndArrowsInterruptInnHealing() { CoreChecks.CombatCannotBeSkippedThroughInnHealing(Data()); }
        [Test] public void RespecRefundHonorsPersistedPointCap() { CoreChecks.TalentRefundCannotOverflowSavedSkillPoints(Data()); }
        [Test] public void InnRestClearsElapsedCombatTimers() { CoreChecks.InnRestActuallyClearsExpiredCombatCooldowns(Data()); }
        [Test] public void FatalFieldMissionsRecoverAtInn() { CoreChecks.FatalFieldMissionDamageAlwaysRespawnsAtInn(Data()); }
        [Test] public void CivilianFieldMissionsRespectTravelBoundaries() { CoreChecks.CivilianFieldTripsCannotBeSilentlyErasedByTravel(Data()); }
        [Test] public void NativeSaveJsonRoundTrip()
        {
            var game = new GameCore(Data()); game.NewGame(2); game.Close(); game.save.weaponStyles[2] = 1; game.save.practiceBits = 7; game.RecordMoment("回家的路", "The road home"); game.PrepareSave();
            var saved = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save)); var loaded = new GameCore(Data(), saved);
            Assert.AreEqual(2, loaded.save.profession); Assert.AreEqual(1, loaded.Style); Assert.AreEqual(7, loaded.save.practiceBits); Assert.AreEqual("回家的路", loaded.save.moments[0].zh);
        }
        private Campaign Data() { return JsonUtility.FromJson<Campaign>(Resources.Load<TextAsset>("Frontier/Campaign").text); }
        [Test] public void ClassesAndPausedMenus() { CoreChecks.ClassesAndPause(Data()); }
        [Test] public void CraftingAndCommissionTransactions() { CoreChecks.Transactions(Data()); }
        [Test] public void FishingFarmingAndCooking() { CoreChecks.DailyLife(Data()); }
        [Test] public void EveryMapAndQuestIsReachable() { CoreChecks.GatesAndGathering(Data()); }
        [Test] public void InvalidSavesAndBoundaryCollision() { CoreChecks.SaveValidation(Data()); }
        [Test] public void StandardDifficultyCampaignAndAllEndings() { CoreChecks.CompleteCampaign(Data()); }
        [Test] public void OldSaveMigrationAndBuildRules() { CoreChecks.MigrationAndBuilds(Data()); }
        [Test] public void AllNewMapsAndTidesAreReachable() { CoreChecks.DistantReachability(Data()); }
        [Test] public void AlternateSkillsRelicCostsAndDailyGifts() { CoreChecks.TechniquesAndTradeoffs(Data()); }
        [Test] public void ClimateContractsAndRaceRules() { CoreChecks.ClimateAndContracts(Data()); }
        [Test] public void AllShorePrioritiesAndCovenants() { CoreChecks.DistantCampaign(Data()); }
        [Test] public void SixFloorExpeditionAndBanking() { CoreChecks.AbyssExpeditions(Data()); }
    }
}
#endif
