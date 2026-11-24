# Checklist kiem thu API thong bao

## Chuan bi
- Chay SQL Server va API .NET 10 theo README.
- Tao hai tai khoan A va B, moi tai khoan co JWT rieng.
- Them mot so ban ghi mau vao bang Notifications cho ca A va B; khong commit token hoac du lieu ca nhan that.

## Ma tran kiem thu

| Ma | Thao tac | Ket qua mong doi | Ket qua thuc te |
|---|---|---|---|
| N01 | Goi GET /api/notifications khong co JWT | 401 | Chua chay |
| N02 | Tai khoan A lay danh sach | 200; chi thong bao UserId=A | Chua chay |
| N03 | Tai khoan A loc isRead=false | 200; tat ca item chua doc; totalCount tinh sau loc | Chua chay |
| N04 | Tai khoan A loc type=Booking | 200; chi thong bao Booking | Chua chay |
| N05 | Ket hop isRead va type | 200; item thoa ca hai dieu kien | Chua chay |
| N06 | page=0 hoac page=-1 | 400 | Chua chay |
| N07 | pageSize=0 hoac pageSize=51 | 400 | Chua chay |
| N08 | page=2147483647&pageSize=50 | 400; chan tran so khi tinh OFFSET | Chua chay |
| N09 | page vuot totalPages | 200; items rong va metadata dung | Chua chay |
| N10 | Page 1 va page 2 khi du lieu khong doi | Khong trung item; thu tu CreatedAt giam dan, Id giam dan | Chua chay |
| N11 | GET /unread-count | So luong chinh xac cua UserId hien tai | Chua chay |
| N12 | A danh dau notification cua A | 204; IsRead=true | Chua chay |
| N13 | A danh dau notification cua B | 404; khong tiet lo notification co ton tai | Chua chay |
| N14 | Danh dau lai notification da doc | 204; idempotent, khong loi | Chua chay |
| N15 | PATCH /read-all | 200; updatedCount dung; chi cap nhat thong bao cua A | Chua chay |
| N16 | Kiem tra EF Core query | Filter, Count, Skip/Take chay tren SQL Server; khong ToList truoc phan trang | Chua chay |

## Ghi nhan ket qua
- Ghi commit SHA va phien ban .NET SDK.
- Ghi request, status code va response thuc te.
- Khong danh dau Dat neu chua gui request that tren SQL Server.
- An JWT va thong tin nhay cam truoc khi dua log len GitHub.
