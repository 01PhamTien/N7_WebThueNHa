# Checklist kiem thu API nha yeu thich

> Day la ke hoach kiem thu. Chi danh dau Passed sau khi chay that tren SQL Server va ghi status code/ket qua thuc te.

## Chuan bi
- Tao tai khoan Tenant A, Tenant B va Landlord; lay JWT rieng.
- Tao mot tin Approved + Published, mot tin Pending/Draft, mot tin Approved + Hidden va mot tin Approved + Closed.
- Dien JWT va PropertyId vao `Requests/favorites.http`.

## Ma tran kiem thu

| Ma | Tinh huong | Ket qua mong doi | Ket qua thuc te |
|---|---|---|---|
| FAV-01 | Goi API khong co JWT | 401 | Chua chay |
| FAV-02 | Tenant them tin Approved + Published | 201; isFavorite=true | Chua chay |
| FAV-03 | Landlord them tin Approved + Published | 201; UserId lay tu Claims | Chua chay |
| FAV-04 | Gui POST hai lan cho cung PropertyId | Lan dau 201, lan sau 200; chi mot ban ghi Favorites | Chua chay |
| FAV-05 | Hai request POST dong thoi cho cung UserId/PropertyId | 201/200; chi mot ban ghi, khong tra 500 | Chua chay |
| FAV-06 | Them tin Pending/Draft hoac Rejected | 404; khong tao Favorite | Chua chay |
| FAV-07 | Them tin Approved + Hidden hoac Closed | 404; khong tao Favorite | Chua chay |
| FAV-08 | Them PropertyId khong ton tai | 404 | Chua chay |
| FAV-09 | Xem danh sach yeu thich | 200; chi UserId hien tai va chi tin dang Published + Approved | Chua chay |
| FAV-10 | page=0, pageSize=0 hoac pageSize=51 | 400 | Chua chay |
| FAV-11 | page=2147483647&pageSize=50 | 400; chan tran so OFFSET | Chua chay |
| FAV-12 | Trang vuot totalPages | 200; Items rong, metadata hop le | Chua chay |
| FAV-13 | Page 1 va page 2 khi du lieu khong doi | Khong trung Property; thu tu CreatedAt/PropertyId giam dan | Chua chay |
| FAV-14 | GET status voi tin da yeu thich | 200; isFavorite=true | Chua chay |
| FAV-15 | GET status voi tin chua yeu thich | 200; isFavorite=false | Chua chay |
| FAV-16 | GET status voi PropertyId khong ton tai | 404 | Chua chay |
| FAV-17 | DELETE tin dang yeu thich | 204; ban ghi cua user hien tai bi xoa | Chua chay |
| FAV-18 | DELETE cung tin nhieu lan | Lan nao cung 204; khong loi | Chua chay |
| FAV-19 | Tin dang yeu thich bi Admin an hoac dong | Tin khong con trong danh sach; ban ghi Favorite duoc giu lai | Chua chay |
| FAV-20 | Tin da bi an duoc Admin khoi phuc ve Published | Tin tu xuat hien lai trong danh sach yeu thich | Chua chay |
| FAV-21 | User A thu xem/xoa Favorite cua User B | Khong doc/xoa duoc; thao tac chi theo Claims cua A | Chua chay |

## Quy tac hien thuc
- UserId luon lay tu JWT Claims; khong nhan UserId tu query/body.
- Chi cho them tin co ModerationStatus=Approved va ListingStatus=Published.
- Khoa chinh kep (UserId, PropertyId) ngan ban ghi trung; duplicate key khi hai request dong thoi duoc xu ly nhu request lap an toan.
- Danh sach chi hien thi tin hien con cong khai. Neu tin bi an/dong, ban ghi Favorite duoc giu lai va se xuat hien lai neu tin duoc khoi phuc.
- API status kiem tra ban ghi Favorite ke ca khi tin dang tam an; DELETE co tinh idempotent.
- Phan trang dung CountAsync, OrderBy, Skip, Take tren IQueryable; khong ToList truoc khi phan trang.

## Ghi nhan
- Ghi commit SHA, .NET SDK, SQL Server va status code thuc te.
- Khong commit JWT hoac du lieu ca nhan that.
