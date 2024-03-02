﻿using BussinessObject;
using DataTransferObject.DTO;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RequestEntity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.Interface
{
    public interface IPartnerRepo
    {
        Task<Partner?> GetPartnerByBookingIdAsync(int bookingId);
        Task<int> CountAppPartnerAsync();
        Task<PartnerServiceDTO?> GetPartnerServiceDetailByIdAsync(int id);
        Task<List<Partner>> GetAllPartnersAsync();
        Task<Partner> GetPartnerByIDAsync(int id);
        Task<Partner> GetPartnerByRefreshTokenAsync(string token);
        Task<Partner?> GetPartnerByEmailAsync(string email);
        Task<Partner> GetPartnerByCodeAsync(string code);
        Task<Partner?> AddPartnereAsync(Partner partner);
        Task<Partner?> GetPartnerByBussinessLicenseAsync(string license);
        Task<Partner?> GetPartnerByDisplayNameAsync(string displayName);
        Task<Partner?> GetPartnerByPartnerNameAsync(string name);
        Task<bool> UpdatePartnerAsync(Partner partner);
        Task<bool> BanPartnerAsync(int partnerId);
        Task<IEnumerable<Partner>> SearchPartnerByPartnerOrServiceNameAsync(string keyword);
        Task<IEnumerable<PartnerService>> GetPartnerServiceByServiceNameAsync(string serviceName, int partnerId);
        Task<PartnerService?> AddPartnerServiceAsync(PartnerService service);
        Task<IEnumerable<Partner>> SearchPartnerByCategoryIdAsync(int categoryId);
        Task<List<RevenuePerWeekDTO>> CalculatePartnerRevenueInMonthAsync(int month,int year);
        Task<List<MonthlyRevenueDTO>> CalculateMonthlyRevenueAsync(int year);
        Task<List<PartnerServiceDTO>> GetPartnerServicesWithBookingCountAsync(int partnerId);
        Task<StatPartnerServiceDTO> CalculateServicesAndRevenueAsync(string? email);
        Task<List<BookingDTO>> GetPartnerBookingsAsync(string partnerEmail, int page, int pageSize);

        Task<List<ChartStatDTO>> GetNewPartnerMonthlyAsync(int year);
    }
}
