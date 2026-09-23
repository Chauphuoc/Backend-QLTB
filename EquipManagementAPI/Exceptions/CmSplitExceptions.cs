using System;

namespace EquipManagementAPI.Exceptions
{
    /// <summary>Không tìm thấy resource (vd: cmPriceId không tồn tại) -> 404</summary>
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }

    /// <summary>Dữ liệu không hợp lệ theo nghiệp vụ (vd: sum ratio != 100) -> 400</summary>
    public class BusinessValidationException : Exception
    {
        public BusinessValidationException(string message) : base(message) { }
    }

    /// <summary>Xung đột dữ liệu (vd: đang có factory_plans dùng operation_code) -> 409</summary>
    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(message) { }
    }
}