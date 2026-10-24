#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace Starfall.Tests
{
    public sealed class UnityCoreTests
    {
        [Test] public void StationedAllyInterposesTwoRealPhysicalStockContacts() { CoreChecks.RealStationedCompanionHasTwoFinitePhysicalStockGuards(Data()); }
        [Test] public void PartySwapClearsPriorCompanionsPhysicalGuardOrder() { CoreChecks.CompanionPartySwapCannotInheritAnotherAllysDefenseOrder(Data()); }
        [Test] public void CompanionAutoAttackCannotFireDeadOrEscapedTargets() { CoreChecks.CompanionCancelledAutoContactNeverFiresAtDeadOrLostTarget(Data()); }
        [Test] public void CacheImpactWarningUsesAuthenticReleasedRaidIntent() { CoreChecks.OutpostWarningsTrackActualCommittedRaiderNotPhantomThreat(Data()); }
        [Test] public void ActiveRangedWatchFindsNewLegitimateFiringLaneAsRaidersMove() { CoreChecks.GuardRangerMovesFromWalkableBlindFootToPhysicalFiringLane(Data()); }
        [Test] public void GuardRangerCanRedirectRealBowReleaseToNewStockThreat() { CoreChecks.GuardedAllyRedirectsPreparedRealArrowToIncomingStockThreat(Data()); }
        [Test] public void DynamicTerrainReroutesARealCompanionWatchInsteadOfWarping() { CoreChecks.GuardReplansBlockedWorldRouteWithoutWarpingOrOrphanOrders(Data()); }
        [Test] public void AlliesPickReachableShootingLanesAtAllEightPhysicalSupplyPosts() { CoreChecks.GuardChoosesRealClearMuzzleCoverageAtEveryRegionalCache(Data()); }
        [Test] public void PhysicalCompanionInterceptsRealIncomingHeavyStockSlam() { CoreChecks.CompanionInterceptsLiveHeavyWindupBeforeIdleCacheRaider(Data()); }
        [Test] public void MedicalGuardRalliesToCriticalHeroOnFootThenReturnsToStock() { CoreChecks.GuardMedicPhysicallyRalliesToCriticalHeroAndReturnsToStock(Data()); }
        [Test] public void CompanionCanPhysicallyGuardActualReliefStockWithoutWarpOrSaveChanges() { CoreChecks.RealCompanionGuardsSuppliesWithoutWarpingOrPersisting(Data()); }
        [Test] public void DefendersCanInterceptAlreadyTelegraphedReliefCacheStrike() { CoreChecks.LateLiveInterceptionProtectsCacheOnRealEnemyRelease(Data()); }
        [Test] public void FlawlessMedalImprovesPhysicalRefugeWithoutHealingActiveCombat() { CoreChecks.FlawlessFieldCareActuallyImprovesSafeTickAndStopsDuringRaids(Data()); }
        [Test] public void FlawlessVeteranSiegesRequireNoAssistsAndPersistEightRealMedals() { CoreChecks.VeteranFlawlessMedalsRequireRealThreeKillsAndAreEarnableOnReplay(Data()); }
        [Test] public void VeteranBarricadeTakesOnlyRealReleasedRaidContactsAndConsumesSuppliesOnce() { CoreChecks.VeteranBarricadeSpendsRealSuppliesAndAbsorbsReleasedStockHits(Data()); }
        [Test] public void VeteranDefenseJournalUsesActualSavedWinsAndPhysicalSiegeMarker() { CoreChecks.VeteranOutpostJournalTracksTrueRegionalFlagsAndLiveObjective(Data()); }
        [Test] public void VeteranReliefDefenseRequiresThreeGenuineKillsAndPreservesV2Saves() { CoreChecks.VeteranReliefDefenseHasThreeRealKillsOneTimeRewardAndSafeReplay(Data()); }
        [Test] public void CompanionActuallyProtectsLiveReliefCacheFromMeleeRaiders() { CoreChecks.CompanionPrioritizesRealOutpostSaboteursOverPassingFoes(Data()); }
        [Test] public void SafeDefendedDesertHighlandPostsProtectAgainstActualClimates() { CoreChecks.DefendedDesertAndHighlandPostsShelterAgainstActualWeather(Data()); }
        [Test] public void DefendedOutpostsEnableGuardedWorldTravelAndCompanionSafeLanding() { CoreChecks.DefendedOutpostRoutesRespectLiveProgressAndSafeLanding(Data()); }
        [Test] public void DefendedEightOutpostsActuallyHealSafeNearbyVisitors() { CoreChecks.DefendedOuterCachesBecomeRealSafeFieldRefuges(Data()); }
        [Test] public void ReliefOutpostsReallyLoseStockAndPatchOnlyOnce() { CoreChecks.OuterPatrolCacheDefenseUsesRealTimedStockCombat(Data()); }
        [Test] public void ReliefOutpostDeadlineAndLeavingRejectFalseWins() { CoreChecks.OuterPatrolRealTimeoutAndPerimeterDoNotForgeWins(Data()); }
        [Test] public void BossWarningCleanupDoesNotEraseZeroOwnedPlayerEffects() { CoreChecks.ZeroIdCombatWarningCleanupCannotEraseUnrelatedPresentation(Data()); }
        [Test] public void BossSupportCallsAreReadableAndCancelableByRealCombat() { CoreChecks.BossReinforcementWindupTelegraphRetractsOnRealControlAndReleasesWithWave(Data()); }
        [Test] public void BossSummonsRequireActualWalkableLocalRoutes() { CoreChecks.BossSummonsRejectVisibleButUnreachableDiagonalPocket(Data()); }
        [Test] public void BossSupportNeverMaterializesOnPlayerCompanion() { CoreChecks.BossSupportCannotSpawnOnTheLivingCompanion(Data()); }
        [Test] public void LateBossRecruitedArcherFiresActualTravelingArrow() { CoreChecks.LateBossArcherActuallyPerformsTimedRealArrowCombat(Data()); }
        [Test] public void LaterBossSummonsMixActualArcherAndMeleeCombat() { CoreChecks.LateBossCyclesSpawnVisibleMeleeAndRangedSupportWithoutActorOverlap(Data()); }
        [Test] public void BossSummonsCannotMaterializeOnActorsOrAcrossSealedWalls() { CoreChecks.BossReinforcementsOccupyFreeReachableLocalFootingOrSkipSafely(Data()); }
        [Test] public void EarnedCounterCommitsOnlyOnActualWeaponRelease() { CoreChecks.EarnedRiposteCommitsOnTrueMeleeArrowOrStaffContact(Data()); }
        [Test] public void ObstructedHeroBowOrStaffDoesNotSpendCounter() { CoreChecks.HeroPhysicalMuzzleObstructionRetainsEarnedRiposte(Data()); }
        [Test] public void GroundBlastsRespectSealedAndReopenedCover() { CoreChecks.DelayedGroundBlastRespectsDynamicCoverAndSafety(Data()); }
        [Test] public void ProtectedDashCannotFarmFalseGroundBlastCounter() { CoreChecks.GroundHazardDodgeRewardRequiresRealExposedStartingFoot(Data()); }
        [Test] public void WallsCannotGrantUnearnedHeavyAttackDodgeCounter() { CoreChecks.HeavyDodgeCounterRequiresRealUnblockedWindupLane(Data()); }
        [Test] public void OverlappingBlastsOnlyDamageThroughClearLineOfSight() { CoreChecks.SimultaneousGroundHazardsOnlyDamageThroughClearLane(Data()); }
        [Test] public void DynamicSealedPlayerRootCancelsAllRealWeaponContacts() { CoreChecks.DynamicTerrainCannotReleaseHeroAttacksFromBuriedRoot(Data()); }
        [Test] public void SealedPlayerGroundRejectsPhantomDashImmunity() { CoreChecks.SealedHeroFootCannotSpendPhantomDodgeOrEarnCounter(Data()); }
        [Test] public void NearInteractionMustNotTargetObjectsThroughWalls() { CoreChecks.RealInteractionSelectionRejectsObjectsBehindSolidWalls(Data()); }
        [Test] public void ExplorerRKeyCannotUnlockSealsBehindSolidWall() { CoreChecks.ExplorationSealCannotBeActivatedThroughRealWall(Data()); }
        [Test] public void CompletedFieldRefugesRequirePhysicalReachNotOnlyRadius() { CoreChecks.PhysicalFieldRefugesDoNotHealAcrossDynamicSolidWall(Data()); }
        [Test] public void DesertShadeCannotProtectPlayerThroughSolidWall() { CoreChecks.DesertShadeRequiresPhysicalContactAcrossLiveWall(Data()); }
        [Test] public void EmbeddedScenePropsStayUsableFromValidNearSide() { CoreChecks.AuthoredWallEmbeddedPropsRemainInteractableFromTheirNearSide(Data()); }
        [Test] public void LiveCivilianRescueConversationsCannotBypassWall() { CoreChecks.ReliefCivilianInteractionsNeedPhysicalOpenLane(Data()); }
        [Test] public void DynamicSealedPlayerRootCancelsAllProfessionSkills() { CoreChecks.DynamicTerrainCannotReleaseHeroSkillsFromBuriedRoot(Data()); }
        [Test] public void InterruptedCounterCannotChargeForUnreleasedAttacks() { CoreChecks.InterruptedCounterWindupsCannotWasteEarnedReward(Data()); }
        [Test] public void HitStopConsumesExactImpactTime() { CoreChecks.ShortHitStopReleasesUnusedFrameTime(Data()); }
        [Test] public void DeadPlayersCannotEnterDelayedFinale() { CoreChecks.BossFinaleCannotInterruptDeathRecovery(Data()); }
        [Test] public void SaveSwitchesResetTransientInputState() { CoreChecks.SwitchingSaveSlotsDoesNotInheritOldAimOrFishingState(Data()); }
        [Test] public void LoadingAnotherSlotDuringModalResumesWorld() { CoreChecks.SwitchingSaveDuringDialogueResumesNewWorldWithoutStaleModal(Data()); }
        [Test] public void PiercingProjectilesPreserveSweptContactOrder() { CoreChecks.PiercingVolleysKeepEveryContactWithReusableTargetSnapshot(Data()); }
        [Test] public void PiercingHitsDistinctActorsWhenSpawnIdsRepeat() { CoreChecks.PiercingProjectilesTrackEnemyIdentityAcrossReusedSpawnIds(Data()); }
        [Test] public void PiercingArrowsHitPhysicalFrontBeforeBackEvenWithReversedRoster() { CoreChecks.PiercingProjectilesContactBodiesInPhysicalEntryOrder(Data()); }
        [Test] public void BossSupportCallWarningsStayWithTheirTrueCaster() { CoreChecks.BossSummonWarningsStayWithPhysicalCasterAcrossReusedIds(Data()); }
        [Test] public void ZeroDamageDoesNotCauseHitStunOrIFrames() { CoreChecks.NonDamagingContactsCannotGrantFreeHitImmunity(Data()); }
        [Test] public void SameCompanionSelectionDoesNotCancelHealing() { CoreChecks.ReselectingSameCompanionPreservesInFlightCommand(Data()); }
        [Test] public void SwitchingCompanionsRefundsOnlyUnspentManualOrders() { CoreChecks.SafeCompanionSwapRefundsUnreleasedButNotSpentOrder(Data()); }
        [Test] public void ForcedTideEvacuationOnlyRefundsUnspentAllyOrders() { CoreChecks.RisingTideRefundsOnlyUnreleasedCompanionOrders(Data()); }
        [Test] public void RisingTideCannotWasteUnfiredAutoBowCooldown() { CoreChecks.RisingTideCancelsUnfiredCompanionAutoShotWithoutFullCadence(Data()); }
        [Test] public void FloodedRescueCivilianAndRaiderResetAllInvalidMotionAndTargets() { CoreChecks.ActualRisingTideClearsStrandedCivilianGaitAndEnemyStockIntent(Data()); }
        [Test] public void RisingTideCannotMintEvacuatedDashCounter() { CoreChecks.ForcedPlayerTideEvacuationCancelsOnlyPhantomDodgeReward(Data()); }
        [Test] public void ForcedTideHeroShoreRecoveryRestartsStableWalkFrames() { CoreChecks.ForcedShorelineRescueResetsHeroWalkCycleWithoutPhantomFootsteps(Data()); }
        [Test] public void InvalidWeaponDamageCannotHealEnemies() { CoreChecks.InvalidOffensiveDamageNeverHealsOrCreatesPhantomHits(Data()); }
        [Test] public void SwappedWeaponsCannotCarrySwordComboProgress() { CoreChecks.RealWeaponChangesRestartWeaponComboButReselectDoesNot(Data()); }
        [Test] public void PracticeExitNeverLeaksRedCirclesOrCounterBuffs() { CoreChecks.PracticeExitRetiresDummyHazardsAndEarnedCounter(Data()); }
        [Test] public void CivicSignalAbortAndTimeoutAreSelective() { CoreChecks.CivicEmberAbortAndFailurePreserveUnrelatedWorldEffects(Data()); }
        [Test] public void NollThreeArrowDrillReallyReflectsHostileShots() { CoreChecks.NollThreeArrowTrainingUsesActualWarriorSkillAndOldSave(Data()); }
        [Test] public void NollPracticeAbortAndTimeoutNeverFarmRewards() { CoreChecks.NollTrainingFailureRetiresOnlyOwnedRealProjectiles(Data()); }
        [Test] public void SwordGuardRipostesRequireActualFrontMeleeContact() { CoreChecks.SwordGuardParriesRealFrontalLungesButNotSlamsOrRearAttacks(Data()); }
        [Test] public void SwordGuardCoolsDownAcrossARealArrowVolley() { CoreChecks.MultipleHostileShotsRespectActualGuardCooldown(Data()); }
        [Test] public void SwordGuardReallyTurnsIncomingArrowsIntoCounterShots() { CoreChecks.SwordGuardReflectsRealHostileShotsWithResourceAndDirection(Data()); }
        [Test] public void SwordGuardRejectsSideHitsAndOtherProfessions() { CoreChecks.SwordGuardDoesNotReflectSideShotsOrBypassClassAndCooldown(Data()); }
        [Test] public void DimHighlandHearthCanBePhysicallyTendedForPermanentService() { CoreChecks.HighlandPublicHearthQualityAndPhysicalTendingImproveRealService(Data()); }
        [Test] public void HighFlameDeliveryEarnsRealStrongerCivicService() { CoreChecks.HighlandFullFlameDeliveryEarnsBrightServiceWithoutMaterials(Data()); }
        [Test] public void HighlandCivicEmberSurvivesActualColdAndGuardCombat() { CoreChecks.HighlandCivicEmberHasActualColdCombatAndPublicChoices(Data()); }
        [Test] public void TemporarilySealedRidgePatrolCanRejoinBeforeFinalReward() { CoreChecks.RidgeWatchMissingBlockedPatrolRejoinsRealThreeGuardObjective(Data()); }
        [Test] public void RestoredRidgeSnapshotsRejectReusedIdAndWrongCombatProfile() { CoreChecks.RidgeWatchWorldSnapshotRejectsImpostorWithReusedPatrolId(Data()); }
        [Test] public void DuplicateActorIdsNeverForgeRidgeWatchCredit() { CoreChecks.RidgeWatchCannotCreditAnotherActorWithReusedNumericId(Data()); }
        [Test] public void LakeKitchenTradeMakesStewAndPermanentFoodUpgrade() { CoreChecks.PublicFishKitchenCreatesRealRepeatableStewAndUpgradedFood(Data()); }
        [Test] public void LakeKitchenTradeRejectsFullBagAndRemoteChoices() { CoreChecks.PublicFishKitchenRejectsStaleChoicesAndInventoryOverflow(Data()); }
        [Test] public void SoupRouteFeedsThreeDistantPhysicalShelters() { CoreChecks.RemoteSoupRouteUsesThreeRealSheltersAndProvidesPhysicalCare(Data()); }
        [Test] public void SoupRouteRejectsRemoteEmptyAndCombatTrades() { CoreChecks.SoupRouteGuardsCachedRemoteAndCombatHandoffs(Data()); }
        [Test] public void RangerAndArcanistQProficiencyHasRealDamageGates() { CoreChecks.RangerAndArcanistAdvancedDrillCountsTrueDistinctSkillContacts(Data()); }
        [Test] public void QProficiencyRejectsRemoteAndMissedCasts() { CoreChecks.AdvancedClassDrillRejectsRemoteAndMissedContacts(Data()); }
        [Test] public void PrecisionLakeFishingAwardsTwoRealFishWithTightTiming() { CoreChecks.PrecisionLakeFishingPaysTwoOnlyForThreeGenuineTightHooks(Data()); }
        [Test] public void PrecisionFishingFailurePreservesOrdinaryLakePlay() { CoreChecks.PrecisionLakeFishingRespectsBagFailuresAndOrdinaryCasting(Data()); }
        [Test] public void LiveHighlandEmberCompassFollowsNextObjectiveAndSave() { CoreChecks.LiveFrostSignalNavigationTracksActualCarriedEmber(Data()); }
        [Test] public void LiveHighlandCachedEmberAndClinicActionsNeedOpenPhysicalLane() { CoreChecks.HighlandEmberCachedChoicesCannotCommitAcrossClosedPosts(Data()); }
        [Test] public void LiveHighlandEmberCannotDrainThroughSealedMountainGuard() { CoreChecks.HighlandEmberEnemyPressureStopsAcrossActualMountainWall(Data()); }
        [Test] public void HighlandGuardsNeverOverlapOtherCombatActors() { CoreChecks.HighlandGuardsSpawnOnDistinctUnoccupiedCombatFeet(Data()); }
        [Test] public void HighlandGuardCannotMaterializeInIsolatedWalkablePocket() { CoreChecks.HighlandSignalGuardsCannotSpawnInIsolatedWalkablePocket(Data()); }
        [Test] public void CombatEffectsKeepEncounterOwnershipThroughImpact() { CoreChecks.CombatSignalsRetireOnlyTheirOwnImpactAndWarningEffects(Data()); }
        [Test] public void MinecartMaterialsReallyLowerSmithCosts() { CoreChecks.MinecartCivicTimberSavingsActuallyReachTownForge(Data()); }
        [Test] public void CartEngineeringHalvesSteamAndAbortsSelectiveActors() { CoreChecks.MinecartEngineeringReallyReducesSteamDamageAndAbortIsSelective(Data()); }
        [Test] public void IronreachEscortRequiresActualFightingAndMovement() { CoreChecks.IronreachMinecartEscortProtectsRealWorkers(Data()); }
        [Test] public void ClosedRailwayWallsInvalidateCachedCartAndDepotTransactions() { CoreChecks.MinecartStaleRailwayActionsCannotSpendOrRewardThroughClosedWall(Data()); }
        [Test] public void IronreachAmbushersNeverSpawnOnCartOrOccupiedActors() { CoreChecks.MinecartJunctionGuardsAvoidActualCombatActorOccupancy(Data()); }
        [Test] public void MinecartPressureRequiresWalkableLaneAndActiveUnstunnedGuard() { CoreChecks.MinecartHullOnlyDrainsFromUnobstructedLivingGuards(Data()); }
        [Test] public void FerrymanRemembersPublicArchiveChoice() { CoreChecks.RestoredFerrymanRespondsToActualCivicArchiveChoice(Data()); }
        [Test] public void PublicFerryRespectsCombatHazardsAndTides() { CoreChecks.RestoredPublicFerryHasRealSafetyAndLowTideGates(Data()); }
        [Test] public void FerryRaiderFeetAvoidSealedPocketsAndLivingAllies() { CoreChecks.FerryAmbushRejectsIsolatedGroundAndOccupiedCompanion(Data()); }
        [Test] public void SealedFerryAmbushCannotSpendEnergyOrCancelMedicalParcel() { CoreChecks.UnavailableFerryAmbushNeverSpendsStaminaOrLosesParcel(Data()); }
        [Test] public void ClosingDockWallInvalidatesAlreadyOpenedFerryChoice() { CoreChecks.PublicFerryStaleDockChoiceCannotCrossClosingPhysicalWall(Data()); }
        [Test] public void DrownedRuinsArchiveChoicesPersist() { CoreChecks.DrownedFloodArchiveRequiresRealTidesAndChoice(Data()); }
        [Test] public void RestoredRootguardHasRealEnemyInterrupts() { CoreChecks.RestoredRootGuardInterruptsOnlyNearbyThreats(Data()); }
        [Test] public void RootsongStalePuzzlesAndGuardCommandsNeedPhysicalContact() { CoreChecks.RootsongStaleToneAndRootguardRequireReachablePhysicalStone(Data()); }
        [Test] public void RootsongRequiresActualTimedForestTones() { CoreChecks.ThornwoodRootsongUsesPhysicalTimingAndPersistentHarmony(Data()); }
        [Test] public void StyleChangesRestartAttackChains() { CoreChecks.StyleChangesRestartCombosButRepeatedStylesPreserveAttacks(Data()); }
        [Test] public void LateAttackBufferHonorsStyleAndWeaponChanges() { CoreChecks.LateBasicAttacksBufferOnceAndRespectCombatProfiles(Data()); }
        [Test] public void RepeatedBufferedLmbDoesNotRetargetOrExtendItsDeadline() { CoreChecks.RepeatedLateLmbCannotReplaceCommittedDirectionOrDeadline(Data()); }
        [Test] public void BufferedLmbFollowsRealSkillRecoveryWithoutCuttingCast() { CoreChecks.LateAttackAfterSkillKeepsWholeBodyRecovery(Data()); }
        [Test] public void TrueHeroDamageCancelsQueuedFollowupButIgnoredHitsDoNot() { CoreChecks.DamageInterruptsLateAttackButSafeContactDoesNot(Data()); }
        [Test] public void RealDamageInterruptsUnspentDodgeWithoutCreatingGhostMobility() { CoreChecks.RealIncomingHitRetiresUnspentDodgeAndLeavesSafeInput(Data()); }
        [Test] public void KeyRepeatDoesNotReaimOrRenewPendingDodge() { CoreChecks.RepeatedLateDodgeCannotResetDeadlineOrDirection(Data()); }
        [Test] public void RangedAttackChainsPayOffWithRealProjectileEffects() { CoreChecks.RangedThirdHitsProduceDistinctRealProjectiles(Data()); }
        [Test] public void CompanionSelfTargetFallsBackToFacing() { CoreChecks.ArcherCompanionCannotWasteVolleyOnItsOwnFootTarget(Data()); }
        [Test] public void StarfireExplodesOnNearSideOfWalls() { CoreChecks.StarfireDetonatesOnNearSideOfRealCover(Data()); }
        [Test] public void NonpiercingArrowsHitPhysicalFrontTarget() { CoreChecks.NonpiercingProjectileContactsFrontEnemyBeforeBackRoster(Data()); }
        [Test] public void AllyAutoVolleyRejectsDeadOrOccludedTarget() { CoreChecks.CompanionAutoFireCannotSpendCadenceOnExpiredEnemy(Data()); }
        [Test] public void EscortArrowContactsUsePhysicalInterceptionOrder() { CoreChecks.EscortArrowsStrikeFirstPhysicalActor(Data()); }
        [Test] public void TravelerDefenseMissingSecondRaiderCannotRemainStuckBehindFirst() { CoreChecks.TravelerFightRejectsMissingSecondRaiderWhileFirstStillLives(Data()); }
        [Test] public void TravelerRaidersAvoidSealedLocalPocketAndLivingCompanion() { CoreChecks.WayfarerAmbushFeetRejectIsolatedPocketAndOccupiedRealCompanion(Data()); }
        [Test] public void TravelerStaleDonationAndSharedRewardNeedUnblockedNPCContact() { CoreChecks.TravelerDonationAndSharedStewRequireLiveUnblockedNPCContact(Data()); }
        [Test] public void EveryRealTravelerAmbushStillResolvesAfterTwoActualLethalHits() { CoreChecks.AllThreeAuthoredTravelersStillSupportRealTwoRaiderRescue(Data()); }
        [Test] public void TravelerEmergencyAidAndRetreatNeedPhysicalNPCContact() { CoreChecks.TravelerEmergencyAidAndWithdrawalRejectClosedNPCContact(Data()); }
        [Test] public void EscortSwordGuardReflectsRealArrow() { CoreChecks.EscortGuardReflectionPreservesRealCivilianSafety(Data()); }
        [Test] public void AlliesDoNotGrantUnearnedHeroWeaponMastery() { CoreChecks.AllyKillsRewardPartyButNotUnwieldedHeroMastery(Data()); }
        [Test] public void CompanionPicksNearestVisibleEnemy() { CoreChecks.CompanionAutoTargetsNearestReachableThreat(Data()); }
        [Test] public void AllyAutoChoosesPhysicallyClearBowLane() { CoreChecks.CompanionAutoSkipsBlockedMuzzleForClearSecondThreat(Data()); }
        [Test] public void AutoArcherTracksMovingEnemyAtActualRelease() { CoreChecks.CompanionAutoReleaseTracksMovingVisibleEnemy(Data()); }
        [Test] public void AllyPhysicalMuzzleObstructionAllowsSafeRetry() { CoreChecks.CompanionAutoMuzzleObstructionRefundsCadenceAndRetries(Data()); }
        [Test] public void ManualArcherVolleyMustClearPhysicalMuzzle() { CoreChecks.ManualCompanionVolleyCannotSpendCooldownFromBlockedMuzzle(Data()); }
        [Test] public void CompanionReleaseReacquiresAfterMuzzleBlockOrTargetDeathAndPreservesManualAim() { CoreChecks.GuardedAllyRetargetsWhenLockedMuzzleBecomesBlocked(Data()); }
        [Test] public void GuardedCompanionRedirectsPreparedShotToRealStockThreat() { CoreChecks.GuardedAllyRedirectsPreparedRealArrowToIncomingStockThreat(Data()); }
        [Test] public void CompanionPrioritizesSoonestAuthenticStockStrike() { CoreChecks.CompanionPrioritizesSoonestAuthenticStockStrike(Data()); }
        [Test] public void StockWarningSelectsFiniteEarliestThreatAndStableTie() { CoreChecks.OuterPatrolWarningSelectsFiniteEarliestThreatAndStableTie(Data()); }
        [Test] public void NonFiniteEnemyWindupsRecoverAndRetractWarnings() { CoreChecks.OuterPatrolInvalidWindupsRecoverAndRetractWarnings(Data()); }
        [Test] public void BothRangerSkillsRequireRealPhysicalMuzzle() { CoreChecks.RangerSkillsCannotLaunchProjectilesFromBuriedPhysicalMuzzle(Data()); }
        [Test] public void AutoHealingRequiresRealLineOfSight() { CoreChecks.CompanionAutoHealRequiresPhysicalReachAndRetiresBlockedRelease(Data()); }
        [Test] public void AllyFeetNeverOverlapHeroOnNarrowBridges() { CoreChecks.CompanionNarrowBridgesKeepSeparateSafeFeet(Data()); }
        [Test] public void CompanionHoldOrderIsPhysicalTransientAndResumesFollowing() { CoreChecks.CompanionHoldPositionIsTransientAndResumesPhysicalFollowing(Data()); }
        [Test] public void CompanionButtonGestureDistinguishesTapFromHoldAndCancelsSafely() { CoreChecks.CompanionButtonGestureDistinguishesTapFromHoldAndCancelsSafely(Data()); }
        [Test] public void FocusComboNeedsPhysicalHeroThenAllyContactAndPersistsOnlyAsTransient() { CoreChecks.FocusComboRequiresTrueHeroThenRealAllyContact(Data()); }
        [Test] public void FocusComboRejectsMissedCastsAndReusedEnemyIdentity() { CoreChecks.FocusComboNeverCreditsMissesImpostorsOrAllyFirst(Data()); }
        [Test] public void ManualCompanionFocusRetainsHeroThreatPriorityAndExactActorIdentity() { CoreChecks.ManualCompanionFocusPrefersMarkedFoeUntilUrgentHeroStrike(Data()); }
        [Test] public void BlockedManualCompanionFocusNeverMarksAndExpires() { CoreChecks.ManualCompanionFocusNeedsAnHonestReleasedMuzzleAndExpires(Data()); }
        [Test] public void CompanionAutoFirePrioritizesCommittedHeroStrikeAtRelease() { CoreChecks.CompanionAutoFirePrioritizesCommittedHeroStrikeAtRelease(Data()); }
        [Test] public void IdleAllyWakesWhenTargetAppears() { CoreChecks.CompanionIdleReadinessReactsToRealTargets(Data()); }
        [Test] public void FullInventoryDoesNotChargeForOverflowStock() { CoreChecks.RepeatablePurchasesCannotConsumeResourcesPastSavedStackCaps(Data()); }
        [Test] public void FieldCampsRespectElapsedCombatTimeAndActiveMissions() { CoreChecks.FieldCampRestAdvancesCombatTimeAndCannotSkipLiveCivilianWork(Data()); }
        [Test] public void FieldCampMenuReflectsSafetyAndWaystationUnlock() { CoreChecks.FieldCampMenuShowsRestOnlyWhenActuallyAvailable(Data()); }
        [Test] public void ActiveRelayCapturesCannotRestThroughTimedWaves() { CoreChecks.LiveGlasswindTrialCannotSkipTheFieldRestClock(Data()); }
        [Test] public void DesertRelayWavesCannotOverlapLiveCombatActors() { CoreChecks.GlasswindBothWavesSpawnAwayFromExistingCombatActors(Data()); }
        [Test] public void CrowdedSecondDesertWaveWaitsWithoutResettingPlayerProgress() { CoreChecks.CrowdedSecondGlasswindWaveWaitsWithoutDiscardingLiveDefense(Data()); }
        [Test] public void GlasswindStaleRelayButtonsRequireActualAccessibleContact() { CoreChecks.GlasswindCachedChoicesRespectClosingPhysicalRelayWall(Data()); }
        [Test] public void ClosingDesertWallStopsRealRelayCaptureAndOpensAgain() { CoreChecks.GlasswindPlayerCannotCaptureRingThroughClosingSolidWall(Data()); }
        [Test] public void RestoredDesertRelayCannotCoolHeroThroughSolidWall() { CoreChecks.GlasswindPublicShadeOnlyProtectsPhysicallyAccessibleTravelers(Data()); }
        [Test] public void ContestedRelayCannotBeJammedByGuardBehindLiveWall() { CoreChecks.RelayCaptureCannotBeJammedThroughClosingRealWall(Data()); }
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
        [Test] public void OverlappingSpellCastersKeepIndependentFourSecondLifetimes() { CoreChecks.PlayerAndCompanionFrostExpireOnIndependentRealWorldClocks(Data()); }
        [Test] public void NewlySealedGroundRetiresOnlyItsOwnFrostSource() { CoreChecks.DynamicTerrainSealsOnlyTheBuriedFrostField(Data()); }
        [Test] public void FrostRecastRetractsOldRingAtActualContact() { CoreChecks.RecastFrostCannotLeaveFalseOldWorldRing(Data()); }
        [Test] public void SenFrostRetainsLongRecoveryAndBreaksAttacks() { CoreChecks.SenCommandPreservesLongEnemyRecoveryAndCancelsRealWindup(Data()); }
        [Test] public void RealDamageBreakNeverShortensExistingEnemyRecovery() { CoreChecks.DamageEarnedBreakDoesNotShortenExistingRecovery(Data()); }
        [Test] public void DynamicGroundSealingCancelsOnlyOwnEnemyWarning() { CoreChecks.SealedEnemyGroundCancelsOnlyItsOwnCombatWarning(Data()); }
        [Test] public void EnemyBowCanSeekExtendedWalkableCornerLanes() { CoreChecks.EnemyArcherFindsMultiTileLateralFiringCorridor(Data()); }
        [Test] public void ClosingLineOfSightCancelsOnlyOccludedArcher() { CoreChecks.OccludedArcherLaneCancelsOnlyItsOwnRealArrow(Data()); }
        [Test] public void BlockedArcherWindupMustRetireUntilLaneReopens() { CoreChecks.OccludedEnemyArcherRetiresUnspentAttackAndRecovers(Data()); }
        [Test] public void OrdinaryEnemyBowRejectsBlockedPhysicalMuzzle() { CoreChecks.EnemyArcherCannotCommitAttackFromBuriedBowHand(Data()); }
        [Test] public void BuriedEnemyArcherCannotReleaseProjectiles() { CoreChecks.SealedEnemyArcherCannotFireUntilGroundReopens(Data()); }
        [Test] public void CompanionFrostCannotCrossLiveSolidWalls() { CoreChecks.CompanionFrostCannotHitThroughWallsEvenWithinAreaRadius(Data()); }
        [Test] public void CompanionAndPlayerFrostCoexistWithPhysicalCasterOrigins() { CoreChecks.CompanionFrostUsesPhysicalCasterAndNeverReplacesArcanistField(Data()); }
        [Test] public void IvoCommandAndAutoVolleyRequireRealCasterGround() { CoreChecks.RangerCompanionCannotShootFromBlockedFloorAndRetriesRealContact(Data()); }
        [Test] public void InvalidCompanionFrostOriginRefundsTheCommand() { CoreChecks.BlockedCompanionFrostCannotTeleportToHeroOrChargeCooldown(Data()); }
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
        [Test] public void AfterlightGuardedReceiversImproveActualLocalServices() { CoreChecks.DefendedReceiversImproveOnlyTheirRealLocalPublicServices(Data()); }
        [Test] public void AfterlightLiveStableChargeAndInstallCannotCrossSealedTerminals() { CoreChecks.AfterlightSealedTerminalsCannotSpendStabilizationOrInstallationMaterials(Data()); }
        [Test] public void AfterlightEmergencyPatchHonorsInventoryAndPerDefenseLimit() { CoreChecks.CivicReserveEmergencyPatchUsesRealSuppliesOncePerActiveDefense(Data()); }
        [Test] public void AfterlightReceiverDamageRequiresRealWindupAndPlayerCanBodyguard() { CoreChecks.CivicReserveSaboteursMustReachReceiverAndCommitRealWindupBeforeDestroyingStock(Data()); }
        [Test] public void AfterlightWorkshopRejectsLiveHostileCrafting() { CoreChecks.AfterlightWorkshopCannotCraftPartsDuringHostileCombat(Data()); }
        [Test] public void AfterlightCivilianReservesDefendAgainstRealTwoWaveAttacks() { CoreChecks.AfterlightCivicDefenseRequiresTwoOwnedCombatWavesAndRealFirstClear(Data()); }
        [Test] public void CivicDefenseWavesAvoidCompanionsAndRealAmbientEnemies() { CoreChecks.CivicWatchPlacementAvoidsBystandersAndCompanionOnRealGround(Data()); }
        [Test] public void CivicDefenseRetainsHonestFirstWaveWhenSecondWaveIsCrowded() { CoreChecks.CivicWatchCrowdedSecondWaveKeepsRealFirstVictoryAndOriginalTimer(Data()); }
        [Test] public void CivicReceiverStaleWatchCommandsRespectClosedPhysicalWall() { CoreChecks.CivicWatchStaleReceiverMenuCannotStartPatchOrWithdrawThroughWall(Data()); }
        [Test] public void AfterlightDefenseWithdrawalLeavesEarnedSupplies() { CoreChecks.AfterlightDefenseRequiresStockAndSupportsHonestWithdrawal(Data()); }
        [Test] public void AfterlightCivicReservesAreRealAndPersist() { CoreChecks.AfterlightReservesRequireHonestPostCharterSuppliesAndImproveRealServices(Data()); }
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
        [Test] public void BeaconRaidersCannotSpawnOnSealedPocketOrRecruitedAlly() { CoreChecks.RoadwardenTwoRealDefendersAvoidSealedPocketAndFriendlyActors(Data()); }
        [Test] public void BeaconLaterWaveWaitsForClearGroundWithoutHealingTwice() { CoreChecks.RoadwardenClearedWaveCanResumeAfterBlockedRealArena(Data()); }
        [Test] public void BeaconStaleStartAndRepairRejectWallBlockedActions() { CoreChecks.BeaconCachedStartAndEnergyRepairRequireOpenPhysicalLane(Data()); }
        [Test] public void RoadwardenSurgeCannotCountSealedPocketAsDodgeExit() { CoreChecks.RoadwardenHazardCannotTreatSealedPocketAsEscapeRoute(Data()); }
        [Test] public void BeaconWithdrawAndAbnormalResetRetireOnlyOwnedCombatActors() { CoreChecks.BeaconWithdrawalCannotEraseFoesRemotelyAndAllResetPathsCleanOwnActors(Data()); }
        [Test] public void FinalRoadwardenCipherCannotMintOathThroughClosingWall() { CoreChecks.RoadwardenCipherRewardCannotBeCollectedThroughClosingWall(Data()); }
        [Test] public void ReliefOutpostCannotLeaveGhostRaidersOrArrowsAfterInterruptedCombat() { CoreChecks.EightRegionOutpostRetiresOrphanRaidersAndOwnArrowsWithoutFakeRescue(Data()); }
        [Test] public void EveryAuthoredOutpostSupportsCompanionSafeTwoRaiderDefense() { CoreChecks.EveryAuthoredOutpostRetainsTwoAccessibleRegionalRaiders(Data()); }
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
        [Test] public void NativeHeroAnimatorCannotSwapClassFacingOrFootPivot()
        {
            // Unity-native EditMode contract. Imported AnimationClip sprites
            // and runtime Sprite.Create wrappers are different objects, but
            // must address the exact SAME accepted pixels and anchored root.
            var method = typeof(FrontierRuntime).GetMethod(
                "HeroSpriteMatchesPublishedFrame",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method, "SpriteRenderer parity gate must exist");
            Texture2D atlas = new Texture2D(512, 256, TextureFormat.RGBA32, false);
            Texture2D other = new Texture2D(512, 256, TextureFormat.RGBA32, false);
            Sprite published = null, equivalent = null, wrongFrame = null;
            Sprite wrongPivot = null, wrongPpu = null, wrongClass = null;
            try
            {
                Vector2 foot = CanonicalHeroSpriteFactory.FootPivot;
                Rect first = new Rect(0, 0, 256, 256);
                published = Sprite.Create(atlas, first, foot, 128f, 0,
                    SpriteMeshType.FullRect);
                equivalent = Sprite.Create(atlas, first, foot, 128f, 0,
                    SpriteMeshType.FullRect);
                wrongFrame = Sprite.Create(atlas, new Rect(256, 0, 256, 256),
                    foot, 128f, 0, SpriteMeshType.FullRect);
                wrongPivot = Sprite.Create(atlas, first, new Vector2(.5f, .5f),
                    128f, 0, SpriteMeshType.FullRect);
                wrongPpu = Sprite.Create(atlas, first, foot, 100f, 0,
                    SpriteMeshType.FullRect);
                wrongClass = Sprite.Create(other, first, foot, 128f, 0,
                    SpriteMeshType.FullRect);
                System.Func<Sprite, bool> matches = sprite =>
                    (bool)method.Invoke(null, new object[] { sprite, published });
                Assert.IsTrue(matches(equivalent), "Same texture/rect/root is equivalent");
                Assert.IsFalse(matches(wrongFrame), "Incorrect facing/contact frame rejected");
                Assert.IsFalse(matches(wrongPivot), "Foot pivot jump rejected");
                Assert.IsFalse(matches(wrongPpu), "Rescaled class rejected");
                Assert.IsFalse(matches(wrongClass), "Wrong profession atlas rejected");
                Assert.IsFalse(matches(null), "No GPU Sprite cannot count as success");
            }
            finally
            {
                if (published != null) Object.DestroyImmediate(published);
                if (equivalent != null) Object.DestroyImmediate(equivalent);
                if (wrongFrame != null) Object.DestroyImmediate(wrongFrame);
                if (wrongPivot != null) Object.DestroyImmediate(wrongPivot);
                if (wrongPpu != null) Object.DestroyImmediate(wrongPpu);
                if (wrongClass != null) Object.DestroyImmediate(wrongClass);
                Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(other);
            }
        }
        private Campaign Data() { return JsonUtility.FromJson<Campaign>(Resources.Load<TextAsset>("Frontier/Campaign").text); }
        [Test] public void ClassesAndPausedMenus() { CoreChecks.ClassesAndPause(Data()); }
        [Test] public void CraftingAndCommissionTransactions() { CoreChecks.Transactions(Data()); }
        [Test] public void FishingFarmingAndCooking() { CoreChecks.DailyLife(Data()); }
        [Test] public void EveryMapAndQuestIsReachable() { CoreChecks.GatesAndGathering(Data()); }
        [Test] public void InvalidSavesAndBoundaryCollision() { CoreChecks.SaveValidation(Data()); }
        [Test] public void StaleDialogueChoicesCannotResolveWorldActionsRemotely() { CoreChecks.StaleDialogueChoicesCannotResolveWorldActionsRemotely(Data()); }
        [Test] public void StaleChainedDialogueChoicesCannotContinueRemotely() { CoreChecks.StaleChainedDialogueChoicesCannotContinueRemotely(Data()); }
        [Test] public void CrossMapObjectReferencesCannotStartInteractions() { CoreChecks.CrossMapObjectReferencesCannotStartInteractions(Data()); }
        [Test] public void RemoteWorldDialogueCannotExecuteChoices() { CoreChecks.RemoteWorldDialogueCannotExecuteChoices(Data()); }
        [Test] public void RemoteImmediateWorldInteractionsRequireReach() { CoreChecks.RemoteImmediateWorldInteractionsRequireReach(Data()); }
        [Test] public void RemoteSpecializedWorldInteractionsRequireReach() { CoreChecks.RemoteSpecializedWorldInteractionsRequireReach(Data()); }
        [Test] public void RemoteExpansionChoicesCannotCommitFromOutOfReachSources() { CoreChecks.RemoteExpansionChoicesCannotCommitFromOutOfReachSources(Data()); }
        [Test] public void RemotePolishChoicesRequireLiveSourceReach() { CoreChecks.RemotePolishChoicesRequireLiveSourceReach(Data()); }
        [Test] public void StandardDifficultyCampaignAndAllEndings() { CoreChecks.CompleteCampaign(Data()); }
        [Test] public void OldSaveMigrationAndBuildRules() { CoreChecks.MigrationAndBuilds(Data()); }
        [Test] public void AllNewMapsAndTidesAreReachable() { CoreChecks.DistantReachability(Data()); }
        [Test] public void AlternateSkillsRelicCostsAndDailyGifts() { CoreChecks.TechniquesAndTradeoffs(Data()); }
        [Test] public void ClimatePulsesKeepRealSecondsAtVariableFPS() { CoreChecks.ClimatePulsesKeepTheirRealWorldSecondAtVariableFPS(Data()); }
        [Test] public void AbyssRiftsKeepRealSixSecondCadence() { CoreChecks.AbyssRiftsKeepTheirSixSecondWorldClock(Data()); }
        [Test] public void RiftWarningsRetainFullDodgeWindow() { CoreChecks.RiftWarningRetainsItsFullVisibleDodgeWindow(Data()); }
        [Test] public void ThermalThresholdsExplainRealDamage() { CoreChecks.ThermalDangerFeedbackRequiresRealExposureAndDamage(Data()); }
        [Test] public void ClimateContractsAndRaceRules() { CoreChecks.ClimateAndContracts(Data()); }
        [Test] public void AllShorePrioritiesAndCovenants() { CoreChecks.DistantCampaign(Data()); }
        [Test] public void SixFloorExpeditionAndBanking() { CoreChecks.AbyssExpeditions(Data()); }
    }
}
#endif
