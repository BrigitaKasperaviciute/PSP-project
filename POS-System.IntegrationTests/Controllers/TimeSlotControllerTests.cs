using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Domain.Entities;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Helpers;
using Xunit;

namespace POS_System.IntegrationTests.Controllers
{
    public class TimeSlotControllerTests : IntegrationTestBase
    {
        private const string BaseUrl = "/api/time-slot";

        [Fact]
        public async Task GetAllTimeSlots_FilteredByAvailability_ReturnsOnlyAvailableTimeSlots()
        {
            // Arrange
            await CreateTimeSlotAsync(new TimeSlotRequestBuilder().WithStartTime(DateTime.UtcNow.AddHours(2)).WithIsAvailable(true).Build());
            await CreateTimeSlotAsync(new TimeSlotRequestBuilder().WithStartTime(DateTime.UtcNow.AddHours(3)).WithIsAvailable(false).Build());

            // Act
            var response = await Client.GetAsync($"{BaseUrl}?onlyAvailable=true&pageSize=10&pageNumber=0");

            // Assert
            AssertOkResponse(response);
            var pagedResponse = await DeserializeResponseAsync<PagedResponse<TimeSlotResponse>>(response);
            pagedResponse.Should().NotBeNull();
            pagedResponse!.Results.Should().NotBeEmpty();
            pagedResponse.Results.Should().Contain(timeSlot => timeSlot!.IsAvailable);

            var persistedTimeSlots = await ExecuteDbAsync(async db => await db.TimeSlots.ToListAsync());
            persistedTimeSlots.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetAllTimeSlots_UnauthenticatedUser_ReturnsUnauthorized()
        {
            // Arrange

            // Act
            var response = await UnauthenticatedClient.GetAsync(BaseUrl);

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetTimeSlotById_ExistingTimeSlot_ReturnsPersistedTimeSlot()
        {
            // Arrange
            var createdTimeSlot = await CreateTimeSlotAsync(TimeSlotRequestBuilder.CreateDefault());

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{createdTimeSlot.Id}");

            // Assert
            AssertOkResponse(response);
            var timeSlot = await DeserializeResponseAsync<TimeSlotResponse>(response);
            timeSlot.Should().NotBeNull();
            timeSlot.Should().BeEquivalentTo(createdTimeSlot);
        }

        [Fact]
        public async Task GetTimeSlotById_UnknownId_ReturnsNotFound()
        {
            // Arrange

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/999999");

            // Assert
            AssertNotFoundResponse(response);
        }

        [Fact]
        public async Task CreateTimeSlot_ValidRequest_PersistsTimeSlotAndReturnsOk()
        {
            // Arrange
            var request = new TimeSlotRequestBuilder()
                .WithEmployeeVersionId(3)
                .WithStartTime(DateTime.UtcNow.AddHours(4))
                .WithIsAvailable(true)
                .Build();

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(request));

            // Assert
            AssertOkResponse(response);
            var createdTimeSlot = await DeserializeResponseAsync<TimeSlotResponse>(response);
            createdTimeSlot.Should().NotBeNull();
            createdTimeSlot!.EmployeeVersionId.Should().Be(request.EmployeeVersionId);
            createdTimeSlot.StartTime.Should().BeCloseTo(request.StartTime, TimeSpan.FromSeconds(1));
            createdTimeSlot.IsAvailable.Should().Be(request.IsAvailable);

            var persistedTimeSlot = await ExecuteDbAsync(async db => await db.TimeSlots.SingleAsync(timeSlot => timeSlot.Id == createdTimeSlot.Id));
            persistedTimeSlot.EmployeeVersionId.Should().Be(request.EmployeeVersionId);
            persistedTimeSlot.StartTime.Should().BeCloseTo(request.StartTime, TimeSpan.FromSeconds(1));
            persistedTimeSlot.IsAvailable.Should().Be(request.IsAvailable);
        }

        [Fact]
        public async Task CreateTimeSlot_NullBody_ReturnsBadRequest()
        {
            // Arrange
            var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

            // Act
            var response = await Client.PostAsync(BaseUrl, content);

            // Assert
            AssertBadRequestResponse(response);
        }

        [Fact]
        public async Task UpdateTimeSlot_ExistingTimeSlot_UpdatesPersistedTimeSlot()
        {
            // Arrange
            var createdTimeSlot = await CreateTimeSlotAsync(new TimeSlotRequestBuilder().WithStartTime(DateTime.UtcNow.AddHours(5)).Build());
            var updateRequest = new TimeSlotRequestBuilder()
                .WithEmployeeVersionId(7)
                .WithStartTime(DateTime.UtcNow.AddHours(6))
                .WithIsAvailable(false)
                .Build();

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/{createdTimeSlot.Id}", CreateJsonContent(updateRequest));

            // Assert
            AssertOkResponse(response);
            var updatedTimeSlot = await DeserializeResponseAsync<TimeSlotResponse>(response);
            updatedTimeSlot.Should().NotBeNull();
            updatedTimeSlot!.EmployeeVersionId.Should().Be(updateRequest.EmployeeVersionId);
            updatedTimeSlot.StartTime.Should().BeCloseTo(updateRequest.StartTime, TimeSpan.FromSeconds(1));
            updatedTimeSlot.IsAvailable.Should().Be(updateRequest.IsAvailable);

            var persistedTimeSlot = await ExecuteDbAsync(async db => await db.TimeSlots.SingleAsync(timeSlot => timeSlot.Id == createdTimeSlot.Id));
            persistedTimeSlot.EmployeeVersionId.Should().Be(updateRequest.EmployeeVersionId);
            persistedTimeSlot.StartTime.Should().BeCloseTo(updateRequest.StartTime, TimeSpan.FromSeconds(1));
            persistedTimeSlot.IsAvailable.Should().Be(updateRequest.IsAvailable);
        }

        [Fact]
        public async Task UpdateTimeSlot_UnknownId_ReturnsNotFound()
        {
            // Arrange
            var request = TimeSlotRequestBuilder.CreateDefault();

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/999999", CreateJsonContent(request));

            // Assert
            AssertNotFoundResponse(response);
        }

        [Fact]
        public async Task DeleteTimeSlot_ExistingTimeSlot_MarksTimeSlotUnavailable()
        {
            // Arrange
            var createdTimeSlot = await CreateTimeSlotAsync(new TimeSlotRequestBuilder().WithIsAvailable(true).Build());

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/{createdTimeSlot.Id}");

            // Assert
            AssertOkResponse(response);
            var deletedTimeSlot = await DeserializeResponseAsync<TimeSlotResponse>(response);
            deletedTimeSlot.Should().NotBeNull();
            deletedTimeSlot!.IsAvailable.Should().BeFalse();

            var persistedTimeSlot = await ExecuteDbAsync(async db => await db.TimeSlots.SingleAsync(timeSlot => timeSlot.Id == createdTimeSlot.Id));
            persistedTimeSlot.IsAvailable.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteTimeSlot_UnknownId_ReturnsNotFound()
        {
            // Arrange

            // Act
            var response = await Client.DeleteAsync($"{BaseUrl}/999999");

            // Assert
            AssertNotFoundResponse(response);
        }

        private async Task<TimeSlotResponse> CreateTimeSlotAsync(TimeSlotRequest request)
        {
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(request));
            AssertOkResponse(response);

            var timeSlot = await DeserializeResponseAsync<TimeSlotResponse>(response);
            timeSlot.Should().NotBeNull();

            var persistedTimeSlot = await ExecuteDbAsync(async db => await db.TimeSlots.OrderByDescending(storedTimeSlot => storedTimeSlot.Id).FirstAsync());

            timeSlot!.Id = persistedTimeSlot.Id;
            return timeSlot;
        }
    }
}