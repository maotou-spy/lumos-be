using BussinessObject;
using BussinessObject.AuthenModel;
using Enum;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Repository;
using Repository.Interface.IUnitOfWork;
using Service.InterfaceService;
using System;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Numerics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Service.Service
{
    public class Authentication : IAuthentication
    {
        private readonly IConfiguration _configuration;
        private readonly IUnitOfWork _unitOfWork;

        public Authentication(IConfiguration configuration, IUnitOfWork unitOfWork)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _unitOfWork = unitOfWork;
        }

        public async Task<(bool, string, string, object,bool, bool)> IsUserAuthenticatedAsync(string email, string password)
        {
            try
            {
                string role = null;
                bool authenticated = false;
                string username = null;
                object userDetails = null;
                bool emailExists = false;
                bool passwordCorrect = false;

                var adminResponse = await _unitOfWork.AdminRepo.GetAdminByEmailAsync(email);
                if (adminResponse != null && adminResponse.Password == password && adminResponse.Status ==1 )
                {
                    passwordCorrect = true;
                    authenticated = true;
                    role = nameof(RolesEnum.Admin);
                    username = "Admin";
                    userDetails = new
                    {
                        ID = adminResponse.AdminId,
                        Email = adminResponse.Email,
                        Code = adminResponse.Code,
                        Role = adminResponse.Role,
                        Status = adminResponse.Status,
                        CreatedDate = adminResponse.CreatedDate,
                        CreatedBy = adminResponse.CreatedBy,
                        LastUpdate = adminResponse.LastUpdate,
                        UpdatedBy = adminResponse.UpdatedBy,
                        ImgUrl = adminResponse.ImgUrl,
                    };
                }
                if (adminResponse != null) { emailExists = true; }

                if (!authenticated)
                {
                    var partnerResponse = await _unitOfWork.PartnerRepo.GetPartnerByEmailAsync(email);
                    if (partnerResponse != null && partnerResponse.Password == password && partnerResponse.Status == 1)
                    {
                        passwordCorrect = true;
                        authenticated = true;
                        role = nameof(RolesEnum.Partner);
                        username = partnerResponse.PartnerName;
                        userDetails = new
                        {
                            ID = partnerResponse.PartnerId,
                            Email = partnerResponse.Email,
                            Code = partnerResponse.Code,
                            Role = partnerResponse.Role,
                            TypeId = partnerResponse.TypeId,
                            DisplayName = partnerResponse.DisplayName,
                            Phone = partnerResponse.Phone,
                            Address = partnerResponse.Address,
                            Status = partnerResponse.Status,
                            CreatedDate = partnerResponse.CreatedDate,
                            CreatedBy = partnerResponse.CreatedBy,
                            LastUpdate = partnerResponse.LastUpdate,
                            UpdatedBy = partnerResponse.UpdatedBy,
                            ImgUrl = partnerResponse.ImgUrl,
                            BusinessLicenseNumber = partnerResponse.BusinessLicenseNumber
                        };
                        
                    }
                    if (partnerResponse != null) { emailExists = true; }   
                }

                if (!authenticated)
                {
                    var customerResponse = await _unitOfWork.CustomerRepo.GetCustomerByEmailAsync(email);
                    if (customerResponse != null && customerResponse.Password == password && customerResponse.Status == 1)
                    {
                        passwordCorrect = true;
                        authenticated = true;
                        role = nameof(RolesEnum.Customer);
                        username = customerResponse.Fullname;
                        userDetails = new
                        {
                            ID = customerResponse.CustomerId,
                            Email = customerResponse.Email,
                            Code = customerResponse.Code,
                            Role = customerResponse.Role,
                            Phone = customerResponse.Phone,
                            Pronounce = customerResponse.Pronounce,
                            Status = customerResponse.Status,
                            CreatedDate = customerResponse.CreatedDate,
                            LastUpdate = customerResponse.LastUpdate,
                            UpdatedBy = customerResponse.UpdateBy,
                            ImgUrl = customerResponse.ImgUrl,
                        };
                    }
                    if (customerResponse != null) { emailExists = true; }
                }

                if (authenticated && role != "Admin")
                {
                    await UpdateLastLoginTime(email);
                }

                return (true, role, username, userDetails, emailExists,passwordCorrect);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in IsUserAuthenticatedAsync: {ex.Message}");
                return (false, null, null, null,false,false);
            }
        }

        public async Task<(string, DateTime, DateTime, string)> GenerateToken(string email, string role)
        {
            try
            {
                var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));
                var signingCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);
                var refreshTokenSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));
                var refreshTokenSigningCredentials = new SigningCredentials(refreshTokenSigningKey, SecurityAlgorithms.HmacSha256);
                var accessTokenExpiration = DateTime.UtcNow.AddHours(3);
                var refreshTokenExpiration = DateTime.UtcNow.AddHours(24);

                var claims = new[]
                {
                    new Claim(ClaimTypes.Email, email),
                    new Claim(ClaimTypes.Role, role),
                };

                var accessTokenOptions = new JwtSecurityToken(
                    claims: claims,
                    expires: accessTokenExpiration,
                    signingCredentials: signingCredentials
                );

                var refreshTokenOptions = new JwtSecurityToken(
                    claims: claims,
                    expires: refreshTokenExpiration,
                    signingCredentials: refreshTokenSigningCredentials
                );

                var accessToken = new JwtSecurityTokenHandler().WriteToken(accessTokenOptions);
                var refreshTokenString = new JwtSecurityTokenHandler().WriteToken(refreshTokenOptions);

                await SaveRefreshTokenToDatabase(email, refreshTokenString);
                var accessTokenexpiration = accessTokenOptions.ValidTo;
                var refreshTokenexpires = refreshTokenOptions.ValidTo;
                return (accessToken, accessTokenexpiration, refreshTokenexpires, refreshTokenString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in GenerateToken: {ex.Message}");
                throw;
            }
        }

        public async Task SaveRefreshTokenToDatabase(string email, string refreshToken)
        {
            try
            {
                var adminResponse = await _unitOfWork.AdminRepo.GetAdminByEmailAsync(email);
                var customerResponse = await _unitOfWork.CustomerRepo.GetCustomerByEmailAsync(email);
                var partnerResponse = await _unitOfWork.PartnerRepo.GetPartnerByEmailAsync(email);

                switch (adminResponse, customerResponse, partnerResponse)
                {
                    case (BussinessObject.Admin admin, _, _):
                        admin.RefreshToken = refreshToken;
                        await UpdateLastLoginTime(email);
                        break;

                    case (_, BussinessObject.Customer customer, _):
                        customer.RefreshToken = refreshToken;
                        await UpdateLastLoginTime(email);
                        break;

                    case (_, _, BussinessObject.Partner partner):
                        partner.RefreshToken = refreshToken;
                        await UpdateLastLoginTime(email);
                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in SaveRefreshTokenToDatabase: {ex.Message}");
                throw;
            }
        }

        public async Task<(bool, string)> ValidateRefreshTokenByEmail(string email)
        {
            try
            {
                var adminResponse = await _unitOfWork.AdminRepo.GetAdminByEmailAsync(email);
                if (adminResponse != null && adminResponse.RefreshToken != null)
                {
                    return (true, adminResponse.RefreshToken);
                }

                var customerResponse = await _unitOfWork.CustomerRepo.GetCustomerByEmailAsync(email);
                if (customerResponse != null && customerResponse.RefreshToken != null)
                {
                    return (true, customerResponse.RefreshToken);
                }

                var partnerResponse = await _unitOfWork.PartnerRepo.GetPartnerByEmailAsync(email);
                if (partnerResponse != null && partnerResponse.RefreshToken != null)
                {
                    return (true, partnerResponse.RefreshToken);
                }

                return (false, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in ValidateRefreshTokenByEmail: {ex.Message}");
                return (false, null);
            }
        }

        public async Task<(bool, string, object)> GetUserdetailsInLoginGooogle(string email)
        {
            try
            {
                object userDetails = null;
                var adminResponse = await _unitOfWork.AdminRepo.GetAdminByEmailAsync(email);
                var customerResponse = await _unitOfWork.CustomerRepo.GetCustomerByEmailAsync(email);
                var partnerResponse = await _unitOfWork.PartnerRepo.GetPartnerByEmailAsync(email);

                if (adminResponse != null && adminResponse.Status == 1)
                {
                    userDetails = new
                    {
                        ID = adminResponse.AdminId,
                        Email = adminResponse.Email,
                        Code = adminResponse.Code,
                        Role = adminResponse.Role,
                        Status = adminResponse.Status,
                        CreatedDate = adminResponse.CreatedDate,
                        CreatedBy = adminResponse.CreatedBy,
                        LastUpdate = adminResponse.LastUpdate,
                        UpdatedBy = adminResponse.UpdatedBy,
                        ImgUrl = adminResponse.ImgUrl,
                    };
                    return (true, nameof(RolesEnum.Admin), userDetails);
                }
                else if (customerResponse != null && customerResponse.Status == 1)
                {
                    userDetails = new
                    {
                        ID = customerResponse.CustomerId,
                        Email = customerResponse.Email,
                        Code = customerResponse.Code,
                        Role = customerResponse.Role,
                        Phone = customerResponse.Phone,
                        Pronounce = customerResponse.Pronounce,
                        Status = customerResponse.Status,
                        CreatedDate = customerResponse.CreatedDate,
                        LastUpdate = customerResponse.LastUpdate,
                        UpdatedBy = customerResponse.UpdateBy,
                        ImgUrl = customerResponse.ImgUrl,
                    };
                    return (true, nameof(RolesEnum.Customer), userDetails);
                }
                else if (partnerResponse != null && partnerResponse.Status == 1)
                {
                    userDetails = new
                    {
                        ID = partnerResponse.PartnerId,
                        Email = partnerResponse.Email,
                        Code = partnerResponse.Code,
                        Role = partnerResponse.Role,
                        TypeId = partnerResponse.TypeId,
                        DisplayName = partnerResponse.DisplayName,
                        Phone = partnerResponse.Phone,
                        Address = partnerResponse.Address,
                        Status = partnerResponse.Status,
                        CreatedDate = partnerResponse.CreatedDate,
                        CreatedBy = partnerResponse.CreatedBy,
                        LastUpdate = partnerResponse.LastUpdate,
                        UpdatedBy = partnerResponse.UpdatedBy,
                        ImgUrl = partnerResponse.ImgUrl,
                        BusinessLicenseNumber = partnerResponse.BusinessLicenseNumber
                    };
                    return (true, nameof(RolesEnum.Partner), userDetails);
                }
                else
                {
                    return (false, null, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in GetUserdetailsInLoginGooogle: {ex.Message}");
                return (false, null, null);
            }
        }
        public async Task<(bool, string)> CheckRole(string email)
        {
            try
            {
                var adminResponse = await _unitOfWork.AdminRepo.GetAdminByEmailAsync(email);
                var customerResponse = await _unitOfWork.CustomerRepo.GetCustomerByEmailAsync(email);
                var partnerResponse = await _unitOfWork.PartnerRepo.GetPartnerByEmailAsync(email);

                if (adminResponse != null)
                {
                    return (true, nameof(RolesEnum.Admin));
                }
                else if (customerResponse != null)
                {
                    return (true, nameof(RolesEnum.Customer));
                }
                else if (partnerResponse != null)
                {
                    return (true, nameof(RolesEnum.Partner));
                }
                else
                {
                    return (false, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in CheckRole: {ex.Message}");
                return (false, null);
            }
        }

        public async Task UpdateLastLoginTime(string email)
        {
            try
            {
                var customerResponse = await _unitOfWork.CustomerRepo.GetCustomerByEmailAsync(email);
                var partnerResponse = await _unitOfWork.PartnerRepo.GetPartnerByEmailAsync(email);

                DateTime now = DateTime.UtcNow;

                switch (customerResponse, partnerResponse)
                {
                    case (BussinessObject.Customer customer, _):
                        customer.LastLogin = now;
                        await _unitOfWork.CustomerRepo.UpdateCustomerAsync(customer);
                        break;

                    case (_, BussinessObject.Partner partner):
                        partner.LastLogin = now;
                        await _unitOfWork.PartnerRepo.UpdatePartnerAsync(partner);
                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in UpdateLastLoginTime: {ex.Message}");
            }
        }


        public async Task<bool> SignOutAsync(string accessToken)
        {
            try
            {
                var (email, roles) = await GetEmailAndRolesFromToken(accessToken);
                if(email.IsNullOrEmpty() || roles.IsNullOrEmpty())
                {
                    throw new Exception("cannot get email or roles");
                }

                string refreshToken = "";
                switch (roles)
                {
                    case nameof(RolesEnum.Admin):
                        Admin admin = await _unitOfWork.AdminRepo.GetAdminByEmailAsync(email);
                        if (admin == null)
                            throw new Exception("cannot find email");

                        refreshToken = await GetRefreshToken(admin.RefreshToken);
                        break;
                    case nameof(RolesEnum.Partner):
                        Partner partner = await _unitOfWork.PartnerRepo.GetPartnerByEmailAsync(email);
                        if (partner == null) throw new Exception("cannot find partner");
                        refreshToken = await GetRefreshToken(partner.RefreshToken);
                        break;
                    case nameof(RolesEnum.Customer):
                        Customer customer = await _unitOfWork.CustomerRepo.GetCustomerByEmailAsync(email);
                        if (customer == null) throw new Exception("cannot find customer");
                        refreshToken = await GetRefreshToken(customer.RefreshToken);
                        break;
                }

                return await Task.FromResult(false);
            }catch(Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        
        private async Task<string> GetRefreshToken(string? refreshToken)
        {
            try
            {
                if (refreshToken == null || refreshToken.Length == 0)
                    throw new Exception();
                return await Task.FromResult(refreshToken);
            }catch( Exception ex )
            {
                throw new Exception("Cannot find user refresh token");
            }
        }

        private async Task<(string?, string?)> GetEmailAndRolesFromToken(string token)
        {
            string aToken = token.Split(" ")[1];
            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            JwtSecurityToken decodedToken = handler.ReadJwtToken(aToken);

            string? email = decodedToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            string? role = decodedToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            return await Task.FromResult((email, role));
        }
    }
}
