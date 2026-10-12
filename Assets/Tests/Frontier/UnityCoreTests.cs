#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace Starfall.Tests
{
    public sealed class UnityCoreTests
    {
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
        [Test] public void LastDodgeFrameFacing(){CoreChecks.DashExitKeepsVisibleFacing(Data());}
        [Test] public void RegionalBossLaneSweeps(){CoreChecks.RegionalBossesSweepAlternatingLanes(Data());}
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
        [Test] public void LiveQuestProgressAndTracking() { CoreChecks.QuestTrackingTransitions(Data()); }
        [Test] public void QuestRoutesRespectCollisionAndTides() { CoreChecks.NavigationRespectsWorldCollision(Data()); }
        [Test] public void SafePracticeAndReplay() { CoreChecks.InteractivePracticeAndReplay(Data()); }
        [Test] public void CorePowersOpenSecretRoutes() { CoreChecks.ExplorationPowersAndDiscoveries(Data()); }
        [Test] public void WeaponStylesAndEnemyCounters() { CoreChecks.CombatStylesAndEnemyCounters(Data()); }
        [Test] public void EncounterSnapshotsAndWorldResponse() { CoreChecks.WorldSnapshotsAndChoiceResponses(Data()); }
        [Test] public void MenuFocusFollowsGeometry() { CoreChecks.PredictableMenuFocus(Data()); }
        [Test] public void OriginalScoresAndFeedback() { CoreChecks.SoundFeedbackAndRegionalScores(Data()); }
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
