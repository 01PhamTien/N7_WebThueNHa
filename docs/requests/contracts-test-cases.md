# Checklist kiem thu API hop dong thue

> Day la ke hoach kiem thu. Chi danh dau Passed sau khi chay that tren SQL Server va ghi status code/ket qua thuc te.

## Chuan bi
- Tao Tenant A, Tenant B, Landlord A va Landlord B.
- Tao Booking Accepted, Pending, Rejected, Cancelled va Expired.
- Booking Accepted phai co StartDate < EndDate va ProposedPrice > 0.
- Dien JWT/BookingId/ContractId vao `Requests/contracts.http`.

## Ma tran kiem thu

| Ma | Tinh huong | Ket qua mong doi | Ket qua thuc te |
|---|---|---|---|
| CT-01 | Khong co JWT goi API | 401 | Chua chay |
| CT-02 | Tenant cua Booking Accepted tao hop dong | 201; Draft; BookingId/PropertyId/LandlordId/TenantId lay tu Booking | Chua chay |
| CT-03 | Landlord cua cung Booking tao lai | 200; tra cung ContractId, khong tao ban ghi thu hai | Chua chay |
| CT-04 | Tao tu Booking Pending/Rejected/Cancelled/Expired | 409 | Chua chay |
| CT-05 | Nguoi khong phai Tenant/Landlord cua Booking tao hop dong | 403 | Chua chay |
| CT-06 | BookingId khong ton tai | 404 | Chua chay |
| CT-07 | Hai request tao hop dong cung Booking dong thoi | 201/200; chi mot RentalContract theo unique BookingId | Chua chay |
| CT-08 | Tenant xem danh sach hop dong | Chi hop dong co TenantId bang Claims | Chua chay |
| CT-09 | Landlord xem danh sach hop dong | Chi hop dong co LandlordId bang Claims | Chua chay |
| CT-10 | Nguoi ngoai xem chi tiet hop dong | 404; khong lo thong tin hop dong | Chua chay |
| CT-11 | page=0, pageSize=0 hoac pageSize=51 | 400 | Chua chay |
| CT-12 | page=2147483647&pageSize=50 | 400; chan tran so OFFSET | Chua chay |
| CT-13 | Page vuot totalPages | 200; Items rong va metadata hop le | Chua chay |
| CT-14 | Tenant goi PUT cap nhat hop dong | 403 | Chua chay |
| CT-15 | Landlord khong so huu hop dong goi PUT | 403 | Chua chay |
| CT-16 | Landlord cap nhat Draft voi du lieu hop le | 200; cap nhat ngay, tien, coc, due day, terms | Chua chay |
| CT-17 | EndDate <= StartDate, MonthlyRent <= 0, DepositAmount < 0, PaymentDueDay ngoai 1..28 | 400 | Chua chay |
| CT-18 | Cap nhat hop dong AwaitingSignature | 409 | Chua chay |
| CT-19 | Landlord submit hop dong Draft | 200; Status=AwaitingSignature | Chua chay |
| CT-20 | Submit hop dong khong con Draft | 409; khong tu dong chuyen Active | Chua chay |
| CT-21 | Landlord ky hop dong AwaitingSignature | 200; chi LandlordSignedAt co gia tri; Status van AwaitingSignature; SignedAt null | Chua chay |
| CT-22 | Tenant ky sau khi Landlord da ky | 200; ca hai timestamp co gia tri; Status=Active; SignedAt bang timestamp muon hon | Chua chay |
| CT-23 | Tenant ky truoc, Landlord ky sau | 200; chi Active khi ca hai timestamp co gia tri | Chua chay |
| CT-24 | Cung mot ben gui lai request ky | 200; idempotent; khong ghi de timestamp lan dau | Chua chay |
| CT-25 | Nguoi khong tham gia hop dong goi sign | 403; khong thay doi du lieu | Chua chay |
| CT-26 | Landlord sua hop dong sau khi mot ben da ky | 200; xoa ca hai timestamp, SignedAt null, Status tro ve Draft; phai submit lai | Chua chay |
| CT-27 | Ky hop dong Draft/Completed/Terminated/Cancelled | 409; khong ghi timestamp | Chua chay |

## Schema chữ ký hai bên
- `RentalContract` có `LandlordSignedAt` và `TenantSignedAt` nullable; `SignedAt` vẫn giữ để lưu thời điểm hoàn tất.
- Mỗi bên chỉ được ghi timestamp của chính mình khi hợp đồng ở `AwaitingSignature`.
- Chỉ khi cả hai timestamp có giá trị mới chuyển `Active`; `SignedAt` bằng thời điểm ký sau cùng.
- Migration chỉ thêm hai cột nullable, không tạo bảng mới và không suy diễn chữ ký lịch sử từ `SignedAt` cũ.
- Khi chỉnh sửa nội dung hợp đồng trước khi Active, phải reset hai timestamp; quy tắc cập nhật chi tiết được kiểm tra ở PR hợp đồng.

## Kiểm thử thời hạn và giá thuê

| Mã | Tình huống | Kết quả mong đợi | Kết quả thực tế |
|---|---|---|---|
| CT-28 | Tạo hợp đồng từ Booking Accepted có khoảng ngày hợp lệ | StartDate/EndDate và MonthlyRent lấy từ Booking; EstimatedRent được tính theo quy ước tháng tròn + ngày lẻ | Chưa chạy |
| CT-29 | Cập nhật hợp đồng Draft với EndDate <= StartDate | 400; không thay đổi hợp đồng | Chưa chạy |
| CT-30 | Cập nhật hợp đồng để chồng lấn Booking Accepted khác trên cùng Property | 409; không thay đổi thời hạn | Chưa chạy |
| CT-31 | Cập nhật hợp đồng để chồng lấn hợp đồng Draft/AwaitingSignature/Active khác trên cùng Property | 409; không thay đổi thời hạn | Chưa chạy |
| CT-32 | Cập nhật hợp đồng với khoảng ngày không chồng lấn | 200; trả EstimatedRent theo thời hạn mới | Chưa chạy |
| CT-33 | Hợp đồng bắt đầu ngày 31/01, kết thúc trước 31/03 | EstimatedRent không bị trôi mốc ngày sau tháng 2 | Chưa chạy |

## Ghi nhan
- Ghi commit SHA, .NET SDK, SQL Server va status code thuc te.
- Khong commit JWT hoac du lieu ca nhan that.
