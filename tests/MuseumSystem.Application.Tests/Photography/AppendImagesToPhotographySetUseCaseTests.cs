using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class AppendImagesToPhotographySetUseCaseTests
{
    [Fact]
    public async Task Valid_append_adds_images_to_existing_set_without_creating_second_set()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyUploadApplicationTestHost.AddPhotographySet(db, artifact);
        await db.SaveChangesAsync();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db);

        var result = await host.AppendUseCase.AppendImagesToPhotographySet(CreatePhotographySetWithImagesUseCaseTests.AppendCommand(
            set.PhotographySetId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "append.jpg", [1, 2, 3])],
            artifactConfirmation: artifact.ArtifactId,
            purposeConfirmation: set.Purpose));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Completed, result.Value!.Status);
        Assert.Equal(set.PhotographySetId, result.Value.PhotographySetId);
        Assert.Equal(1, await db.PhotographySets.CountAsync());
        var image = await db.ArtifactImages.SingleAsync();
        Assert.Equal(set.PhotographySetId, image.PhotographySetId);
    }

    [Fact]
    public async Task Set_not_found_is_rejected_before_storage()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);

        var result = await host.AppendUseCase.AppendImagesToPhotographySet(CreatePhotographySetWithImagesUseCaseTests.AppendCommand(
            Guid.NewGuid(),
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "append.jpg", [1, 2, 3])]));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "PhotographySet.NotFound");
        Assert.Empty(storage.StoreOriginalCalls);
        Assert.Equal(0, await db.PhotographyUploadOperations.CountAsync());
    }

    [Fact]
    public async Task Artifact_confirmation_match_is_accepted_and_conflict_blocks_entire_command()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyUploadApplicationTestHost.AddPhotographySet(db, artifact);
        await db.SaveChangesAsync();
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);

        var conflict = await host.AppendUseCase.AppendImagesToPhotographySet(CreatePhotographySetWithImagesUseCaseTests.AppendCommand(
            set.PhotographySetId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "append.jpg", [1])],
            artifactConfirmation: Guid.NewGuid()));

        Assert.False(conflict.Succeeded);
        Assert.Contains(conflict.ValidationIssues, issue => issue.Code == "PhotographySet.ArtifactConflict");
        Assert.Empty(storage.StoreOriginalCalls);
        Assert.Equal(0, await db.ArtifactImages.CountAsync());

        var match = await host.AppendUseCase.AppendImagesToPhotographySet(CreatePhotographySetWithImagesUseCaseTests.AppendCommand(
            set.PhotographySetId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "append.jpg", [1])],
            idempotencyKey: "append-key-match",
            artifactConfirmation: artifact.ArtifactId));

        Assert.True(match.Succeeded);
        Assert.Single(storage.StoreOriginalCalls);
    }

    [Fact]
    public async Task Purpose_confirmation_match_is_accepted_and_conflict_blocks_entire_command()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyUploadApplicationTestHost.AddPhotographySet(db, artifact, PhotographyPurpose.PreMaintenance);
        await db.SaveChangesAsync();
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);

        var conflict = await host.AppendUseCase.AppendImagesToPhotographySet(CreatePhotographySetWithImagesUseCaseTests.AppendCommand(
            set.PhotographySetId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "append.jpg", [1])],
            purposeConfirmation: PhotographyPurpose.PostMaintenance));

        Assert.False(conflict.Succeeded);
        Assert.Contains(conflict.ValidationIssues, issue => issue.Code == "PhotographySet.PurposeConflict");
        Assert.Empty(storage.StoreOriginalCalls);

        var match = await host.AppendUseCase.AppendImagesToPhotographySet(CreatePhotographySetWithImagesUseCaseTests.AppendCommand(
            set.PhotographySetId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "append.jpg", [1])],
            idempotencyKey: "append-key-purpose-match",
            purposeConfirmation: PhotographyPurpose.PreMaintenance));

        Assert.True(match.Succeeded);
        Assert.Single(storage.StoreOriginalCalls);
    }

    [Fact]
    public async Task Append_never_mutates_set_context_and_keeps_custody_state_unchanged()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Lab");
        var set = PhotographyUploadApplicationTestHost.AddPhotographySet(db, artifact, PhotographyPurpose.DuringMaintenance);
        await db.SaveChangesAsync();
        var originalDate = set.PhotographyDate;
        var originalPhotographer = set.PhotographerUserId;
        var originalStatus = artifact.CurrentStatus;
        var originalHolderType = artifact.CurrentHolderType;
        var originalHolderName = artifact.CurrentHolderName;
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db);

        var result = await host.AppendUseCase.AppendImagesToPhotographySet(CreatePhotographySetWithImagesUseCaseTests.AppendCommand(
            set.PhotographySetId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "append.jpg", [1, 2, 3])]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyPurpose.DuringMaintenance, set.Purpose);
        Assert.Equal(originalDate, set.PhotographyDate);
        Assert.Equal(originalPhotographer, set.PhotographerUserId);
        Assert.Equal(originalStatus, artifact.CurrentStatus);
        Assert.Equal(originalHolderType, artifact.CurrentHolderType);
        Assert.Equal(originalHolderName, artifact.CurrentHolderName);
    }

    [Fact]
    public async Task Mixed_append_partial_success_keeps_existing_set_and_file_level_results()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyUploadApplicationTestHost.AddPhotographySet(db, artifact);
        await db.SaveChangesAsync();
        var processor = new FakeArtifactImageProcessor();
        processor.Reject("bad.txt", "Unsupported file type.");
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, processor);

        var result = await host.AppendUseCase.AppendImagesToPhotographySet(CreatePhotographySetWithImagesUseCaseTests.AppendCommand(
            set.PhotographySetId,
            [
                CreatePhotographySetWithImagesUseCaseTests.File(0, "append.jpg", [1, 2, 3]),
                CreatePhotographySetWithImagesUseCaseTests.File(1, "bad.txt", [9])
            ]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.CompletedWithFailures, result.Value!.Status);
        Assert.Equal([PhotographyUploadFileOutcomeStatus.Succeeded, PhotographyUploadFileOutcomeStatus.Rejected], result.Value.FileResults.OrderBy(file => file.ClientFileOrdinal).Select(file => file.Status).ToArray());
        Assert.Equal(1, await db.PhotographySets.CountAsync());
        Assert.Equal(1, await db.ArtifactImages.CountAsync());
    }
}
