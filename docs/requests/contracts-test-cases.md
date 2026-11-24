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
| CT-21 | Kiem tra chu ky hai ben | Hien tai chua co endpoint Active; cho schema co LandlordSignedAt/TenantSignedAt | Chua chay |

## Gioi han schema da thong nhat
- RentalContract hien chi co mot cot SignedAt chung, khong xac dinh rieng ben nao da ky.
- Khong them cot/migration trong PR nay.
- API chi tao Draft, cap nhat Draft va chuyen AwaitingSignature.
- Khong co API tu dong chuyen Active cho den khi schema luu duoc xac nhan cua ca Landlord va Tenant.

## Ghi nhan
- Ghi commit SHA, .NET SDK, SQL Server va status code thuc te.
- Khong commit JWT hoac du lieu ca nhan that.
