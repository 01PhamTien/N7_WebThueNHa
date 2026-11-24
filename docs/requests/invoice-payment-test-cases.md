# Checklist kiem thu API hoa don va thanh toan thu cong

> Chỉ đánh dấu Passed sau khi chạy request thật trên SQL Server test và ghi status code/response.

## Chuẩn bị
- Tạo Tenant A, Tenant B, Landlord A, Landlord B và Admin.
- Tạo hợp đồng Active (đã có LandlordSignedAt và TenantSignedAt), hợp đồng Draft và hợp đồng AwaitingSignature.
- Tạo hóa đơn Deposit, MonthlyRent và Other; dùng invoice item có số lượng/đơn giá khác nhau.
- Điền JWT/ContractId/InvoiceId/PaymentId trong `Requests/invoices-payments.http`.

## Ma trận kiểm thử

| Mã | Tình huống | Kết quả mong đợi | Kết quả thực tế |
|---|---|---|---|
| INV-01 | Không JWT xem hóa đơn | 401 | Chưa chạy |
| INV-02 | Tenant xem danh sách hóa đơn | Chỉ hóa đơn thuộc hợp đồng của Tenant | Chưa chạy |
| INV-03 | Landlord xem danh sách hóa đơn | Chỉ hóa đơn thuộc hợp đồng Landlord quản lý | Chưa chạy |
| INV-04 | Người ngoài xem chi tiết hóa đơn | 404, không lộ dữ liệu | Chưa chạy |
| INV-05 | Landlord tạo hóa đơn cho hợp đồng Draft/AwaitingSignature | 409 | Chưa chạy |
| INV-06 | Tenant gọi API tạo hóa đơn | 403 | Chưa chạy |
| INV-07 | Deposit có tổng khác DepositAmount của hợp đồng | 400 | Chưa chạy |
| INV-08 | Deposit không có khoản cọc hoặc có kỳ thanh toán | 409/400 | Chưa chạy |
| INV-09 | MonthlyRent thiếu kỳ hoặc kỳ ngoài hợp đồng | 400 | Chưa chạy |
| INV-10 | Dòng hóa đơn có quantity/unitPrice không hợp lệ | 400 | Chưa chạy |
| INV-11 | Tổng hóa đơn | Server tính tổng từ InvoiceItems, không nhận TotalAmount từ client | Chưa chạy |
| INV-12 | Tạo hai hóa đơn Deposit chưa hủy cho cùng hợp đồng | Lần hai 409; unique index bảo vệ đồng thời | Chưa chạy |
| INV-13 | Tạo hai hóa đơn MonthlyRent cùng kỳ | Lần hai 409; unique index bảo vệ đồng thời | Chưa chạy |
| PAY-01 | Tenant gửi thanh toán Cash/BankTransfer | 201; trạng thái Pending | Chưa chạy |
| PAY-02 | Tenant gửi VNPay/MoMo qua luồng thủ công | 400 | Chưa chạy |
| PAY-03 | Người không phải Tenant của hợp đồng tạo payment | 404 | Chưa chạy |
| PAY-04 | Amount <= 0 hoặc có hơn 2 chữ số thập phân | 400 | Chưa chạy |
| PAY-05 | Tổng Pending + Succeeded vượt TotalAmount | 409 | Chưa chạy |
| PAY-06 | TransactionCode đã dùng ở payment khác | 409 | Chưa chạy |
| PAY-07 | Landlord khác hợp đồng xác nhận/từ chối | 403 | Chưa chạy |
| PAY-08 | Admin xác nhận payment Pending | 200; lưu ReviewedById/ReviewedAt và PaidAt | Chưa chạy |
| PAY-09 | Landlord xác nhận payment Pending của hợp đồng mình | 200; trạng thái Succeeded | Chưa chạy |
| PAY-10 | Xác nhận payment không còn Pending | 409 | Chưa chạy |
| PAY-11 | Xác nhận làm tổng Succeeded vượt hóa đơn | 409 | Chưa chạy |
| PAY-12 | Xác nhận thanh toán một phần | InvoiceStatus=PartiallyPaid | Chưa chạy |
| PAY-13 | Xác nhận đủ tiền | InvoiceStatus=Paid | Chưa chạy |
| PAY-14 | Reject payment Pending | Failed; lưu FailureReason, ReviewedById, ReviewedAt | Chưa chạy |
| PAY-15 | Reject không có lý do | 400 | Chưa chạy |
| PAY-16 | Payment Pending bị reject rồi tạo payment mới | Số tiền Pending cũ không còn được giữ chỗ | Chưa chạy |
| PAY-17 | Hai request xác nhận đồng thời | Không thể ghi nhận vượt tổng hóa đơn | Chưa chạy |
| PAY-18 | Hóa đơn quá hạn chưa thanh toán đủ | InvoiceStatus=Overdue | Chưa chạy |

## Quy tắc schema
- Dùng bảng `Invoices`, `InvoiceItems`, `Payments` và `AppDbContext` hiện có.
- Không tạo PaymentDbContext hoặc bảng Payments mới.
- `Payment.ReviewedById` và `ReviewedAt` lưu người/thời điểm duyệt; `FailureReason` lưu lý do từ chối.
- Unique filtered index ngăn hóa đơn Deposit trùng và MonthlyRent trùng kỳ (trừ hóa đơn đã Cancelled).
- Payment thủ công chỉ cho phép Cash/BankTransfer; client không được tự đánh dấu Succeeded.
- Chỉ Landlord sở hữu hợp đồng hoặc Admin được duyệt/từ chối.
- Không đánh dấu kiểm thử SQL Server là Passed nếu chưa chạy thực tế.

## Ghi nhận
- Ghi commit SHA, .NET SDK, SQL Server và status code thực tế.
- Không commit JWT, connection string hoặc dữ liệu cá nhân thật.
