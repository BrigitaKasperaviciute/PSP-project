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
    public class ServiceReservationControllerTests : IntegrationTestBase
    {
        private const string BaseUrl = "/api/service-reservation";

        [Fact]
        public async Task GetAllServiceReservations_WithCreatedReservation_ReturnsPagedResults()
        {
            // Arrange
            var prerequisites = await CreateReservationPrerequisitesAsync();
            var reservation = await CreateServiceReservationAsync(new ServiceReservationRequestBuilder()
                .WithCartItemId(prerequisites.CartItemId)
                .WithTimeSlotId(prerequisites.TimeSlotId)
                .WithCustomerName("John Doe")
                .WithCustomerPhone("1234567890")
                .WithBookingTime(DateTime.UtcNow.AddDays(1))
                .Build());

            // Act
            var response = await Client.GetAsync(BaseUrl);

            // Assert
            AssertOkResponse(response);
            var pagedResponse = await DeserializeResponseAsync<PagedResponse<ServiceReservationResponse>>(response);
            pagedResponse.Should().NotBeNull();
            pagedResponse!.Results.Should().ContainSingle(item => item!.Id == reservation.Id);

            var persistedReservationCount = await ExecuteDbAsync(async db => await db.ServiceReservations.CountAsync());
            persistedReservationCount.Should().Be(1);
        }

        [Fact]
        public async Task GetAllServiceReservations_UnauthenticatedUser_ReturnsUnauthorized()
        {
            // Arrange

            // Act
            var response = await UnauthenticatedClient.GetAsync(BaseUrl);

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetServiceReservationById_ExistingReservation_ReturnsPersistedReservation()
        {
            // Arrange
            var prerequisites = await CreateReservationPrerequisitesAsync();
            var createdReservation = await CreateServiceReservationAsync(new ServiceReservationRequestBuilder()
                .WithCartItemId(prerequisites.CartItemId)
                .WithTimeSlotId(prerequisites.TimeSlotId)
                .Build());

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/{createdReservation.Id}");

            // Assert
            AssertOkResponse(response);
            var serviceReservation = await DeserializeResponseAsync<ServiceReservationResponse>(response);
            serviceReservation.Should().NotBeNull();
            serviceReservation!.Id.Should().Be(createdReservation.Id);
            serviceReservation.CartItemId.Should().Be(createdReservation.CartItemId);
            serviceReservation.TimeSlotId.Should().Be(createdReservation.TimeSlotId);
            serviceReservation.CustomerName.Should().Be(createdReservation.CustomerName);
            serviceReservation.CustomerPhone.Should().Be(createdReservation.CustomerPhone);
            serviceReservation.IsCancelled.Should().Be(createdReservation.IsCancelled);
        }

        [Fact]
        public async Task GetServiceReservationById_UnknownId_ReturnsNotFound()
        {
            // Arrange

            // Act
            var response = await Client.GetAsync($"{BaseUrl}/999999");

            // Assert
            AssertNotFoundResponse(response);
        }

        [Fact]
        public async Task CreateServiceReservation_ValidRequest_PersistsReservationAndDisablesTimeSlot()
        {
            // Arrange
            var prerequisites = await CreateReservationPrerequisitesAsync();
            var request = new ServiceReservationRequestBuilder()
                .WithCartItemId(prerequisites.CartItemId)
                .WithTimeSlotId(prerequisites.TimeSlotId)
                .WithBookingTime(DateTime.UtcNow.AddDays(2))
                .WithCustomerName("Reservation Customer")
                .WithCustomerPhone("987654321")
                .WithIsCancelled(false)
                .Build();

            // Act
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(request));

            // Assert
            AssertOkResponse(response);
            var serviceReservation = await DeserializeResponseAsync<ServiceReservationResponse>(response);
            serviceReservation.Should().NotBeNull();
            serviceReservation!.CartItemId.Should().Be(request.CartItemId);
            serviceReservation.TimeSlotId.Should().Be(request.TimeSlotId);
            serviceReservation.CustomerName.Should().Be(request.CustomerName);
            serviceReservation.CustomerPhone.Should().Be(request.CustomerPhone);

            var persistedReservation = await ExecuteDbAsync(async db => await db.ServiceReservations.SingleAsync(reservation => reservation.Id == serviceReservation.Id));
            persistedReservation.CartItemId.Should().Be(request.CartItemId);
            persistedReservation.TimeSlotId.Should().Be(request.TimeSlotId);
            persistedReservation.CustomerName.Should().Be(request.CustomerName);

            var persistedTimeSlot = await ExecuteDbAsync(async db => await db.TimeSlots.SingleAsync(timeSlot => timeSlot.Id == prerequisites.TimeSlotId));
            persistedTimeSlot.IsAvailable.Should().BeFalse();
        }

        [Fact]
        public async Task CreateServiceReservation_NullBody_ReturnsBadRequest()
        {
            // Arrange
            var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

            // Act
            var response = await Client.PostAsync(BaseUrl, content);

            // Assert
            AssertBadRequestResponse(response);
        }

        [Fact]
        public async Task UpdateServiceReservation_ExistingReservation_UpdatesPersistedReservation()
        {
            // Arrange
            var prerequisites = await CreateReservationPrerequisitesAsync();
            var createdReservation = await CreateServiceReservationAsync(new ServiceReservationRequestBuilder()
                .WithCartItemId(prerequisites.CartItemId)
                .WithTimeSlotId(prerequisites.TimeSlotId)
                .WithCustomerName("Original Customer")
                .WithCustomerPhone("111111111")
                .Build());

            var updateRequest = new ServiceReservationRequestBuilder()
                .WithCartItemId(prerequisites.CartItemId)
                .WithTimeSlotId(prerequisites.TimeSlotId)
                .WithBookingTime(DateTime.UtcNow.AddDays(3))
                .WithCustomerName("Updated Customer")
                .WithCustomerPhone("222222222")
                .WithIsCancelled(true)
                .Build();

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/{createdReservation.Id}", CreateJsonContent(updateRequest));

            // Assert
            AssertOkResponse(response);
            var updatedReservation = await DeserializeResponseAsync<ServiceReservationResponse>(response);
            updatedReservation.Should().NotBeNull();
            updatedReservation!.CustomerName.Should().Be(updateRequest.CustomerName);
            updatedReservation.CustomerPhone.Should().Be(updateRequest.CustomerPhone);
            updatedReservation.IsCancelled.Should().Be(updateRequest.IsCancelled);

            var persistedReservation = await ExecuteDbAsync(async db => await db.ServiceReservations.SingleAsync(reservation => reservation.Id == createdReservation.Id));
            persistedReservation.CustomerName.Should().Be(updateRequest.CustomerName);
            persistedReservation.CustomerPhone.Should().Be(updateRequest.CustomerPhone);
            persistedReservation.isCancelled.Should().Be(updateRequest.IsCancelled);
        }

        [Fact]
        public async Task UpdateServiceReservation_UnknownId_ReturnsNotFound()
        {
            // Arrange
            var request = ServiceReservationRequestBuilder.CreateDefault();

            // Act
            var response = await Client.PutAsync($"{BaseUrl}/999999", CreateJsonContent(request));

            // Assert
            AssertNotFoundResponse(response);
        }

        private async Task<ServiceReservationResponse> CreateServiceReservationAsync(ServiceReservationRequest request)
        {
            var response = await Client.PostAsync(BaseUrl, CreateJsonContent(request));
            AssertOkResponse(response);

            var reservation = await DeserializeResponseAsync<ServiceReservationResponse>(response);
            reservation.Should().NotBeNull();

            var persistedReservation = await ExecuteDbAsync(async db => await db.ServiceReservations.OrderByDescending(serviceReservation => serviceReservation.Id).FirstAsync());

            reservation!.Id = persistedReservation.Id;
            return reservation;
        }

        private async Task<(int CartItemId, int TimeSlotId)> CreateReservationPrerequisitesAsync()
        {
            var timeSlot = await CreateTimeSlotAsync(new TimeSlotRequestBuilder()
                .WithEmployeeVersionId(5)
                .WithStartTime(DateTime.UtcNow.AddHours(8))
                .WithIsAvailable(true)
                .Build());

            var cartItemId = await ExecuteDbAsync(async db =>
            {
                var cartItem = new CartItem
                {
                    CartId = 1,
                    Quantity = 1,
                    IsProduct = false,
                    IsDeleted = false,
                    ProductVersionId = null,
                    ServiceVersionId = 1
                };

                db.CartItems.Add(cartItem);
                await db.SaveChangesAsync();
                return cartItem.Id;
            });

            return (cartItemId, timeSlot.Id);
        }

        private async Task<TimeSlotResponse> CreateTimeSlotAsync(TimeSlotRequest request)
        {
            var response = await Client.PostAsync("/api/time-slot", CreateJsonContent(request));
            AssertOkResponse(response);

            var timeSlot = await DeserializeResponseAsync<TimeSlotResponse>(response);
            timeSlot.Should().NotBeNull();
            return timeSlot!;
        }
    }
}