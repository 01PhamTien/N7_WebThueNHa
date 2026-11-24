# Request mẫu – API quản lý nhà

Base URL khi chạy local: `https://localhost:<port>`

Các API có `[Authorize(Roles = "Landlord")]` cần header:

```http
Authorization: Bearer <accessToken>
Content-Type: application/json
```

Lấy token bằng `POST /api/auth/login`. Tài khoản gọi API ghi phải có role `Landlord`.

## 1. Danh sách tin đang công khai

```http
GET /api/properties?keyword=phong&province=Hue&propertyTypeId=1&minPrice=1000000&maxPrice=5000000&minArea=15&maxArea=40&minBedrooms=1&page=1&pageSize=10
```

Chỉ trả tin có `ListingStatus=Published` và `ModerationStatus=Approved`.

## 2. Chi tiết tin

```http
GET /api/properties/{propertyId}
```

Tin chưa công khai chỉ xem được bởi chủ sở hữu hoặc Admin.

## 3. Danh sách tin của tôi (có phân trang server-side)

Mặc định `page=1`, `pageSize=10`; `pageSize` tối đa 50. Có thể lọc theo trạng thái tin bằng enum `status` (ví dụ `Draft`, `Published`, `Hidden`, `Closed`). Không truyền `LandlordId`: API luôn lấy chủ nhà từ JWT.

```http
GET /api/properties/mine?page=1&pageSize=10&status=Draft
Authorization: Bearer <landlordAccessToken>
```

Ví dụ response thành công:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 10,
  "totalCount": 0,
  "totalPages": 0,
  "hasPrevious": false,
  "hasNext": false
}
```

Tham số `page < 1`, `pageSize < 1`, `pageSize > 50` hoặc `status` không hợp lệ trả về `400`. Trang vượt quá `totalPages` trả danh sách `items` rỗng cùng metadata hợp lệ.

## 4. Tạo tin

```http
POST /api/properties
Authorization: Bearer <landlordAccessToken>
Content-Type: application/json

{
  "propertyTypeId": 1,
  "title": "Phòng trọ gần trung tâm",
  "description": "Phòng có cửa sổ, chỗ để xe.",
  "pricePerMonth": 2500000,
  "area": 22,
  "bedrooms": 1,
  "bathrooms": 1,
  "addressLine": "12 đường mẫu",
  "wardName": "Phường mẫu",
  "provinceName": "Thành phố Huế",
  "latitude": 16.4637,
  "longitude": 107.5909
}
```

Tin mới được tạo ở trạng thái `Draft` và `Pending`, cần được duyệt trước khi công khai.

## 5. Cập nhật tin

```http
PUT /api/properties/{propertyId}
Authorization: Bearer <landlordAccessToken>
Content-Type: application/json

{
  "propertyTypeId": 1,
  "title": "Phòng trọ đã cập nhật",
  "description": "Mô tả mới.",
  "pricePerMonth": 2700000,
  "area": 24,
  "bedrooms": 1,
  "bathrooms": 1,
  "addressLine": "12 đường mẫu",
  "wardName": "Phường mẫu",
  "provinceName": "Thành phố Huế",
  "latitude": 16.4637,
  "longitude": 107.5909
}
```

Sau khi sửa, tin được chuyển về `Draft`/`Pending` để chờ duyệt lại.

## 6. Ẩn, đăng lại hoặc đóng tin

```http
PATCH /api/properties/{propertyId}/status
Authorization: Bearer <landlordAccessToken>
Content-Type: application/json

{ "status": 2 }
```

Giá trị enum: `1 = Published`, `2 = Hidden`, `3 = Closed`. Chỉ tin đã được duyệt mới chuyển sang `Published`.

## 7. Thêm hình ảnh

```http
POST /api/properties/{propertyId}/images
Authorization: Bearer <landlordAccessToken>
Content-Type: application/json

{ "imageUrl": "https://example.com/property-1.jpg" }
```

URL phải dùng HTTPS; mỗi tin tối đa 20 hình. Hình đầu tiên tự động là ảnh chính.

## 8. Chọn ảnh chính

```http
PUT /api/properties/{propertyId}/images/{imageId}/primary
Authorization: Bearer <landlordAccessToken>
```

## 9. Xóa ảnh

```http
DELETE /api/properties/{propertyId}/images/{imageId}
Authorization: Bearer <landlordAccessToken>
```

Nếu xóa ảnh chính, ảnh còn lại có thứ tự hiển thị nhỏ nhất sẽ được chọn làm ảnh chính.

## Mã lỗi thường gặp

- `400`: dữ liệu hoặc tham số lọc không hợp lệ.
- `401`: thiếu hoặc token không hợp lệ.
- `403`: không phải chủ sở hữu tin.
- `404`: tin/hình ảnh không tồn tại hoặc tin chưa công khai với người xem thông thường.
- `409`: tin chưa được duyệt để đăng hoặc vượt giới hạn ảnh.

Lưu ý: API quản lý ảnh hiện lưu URL ảnh do client cung cấp, chưa triển khai upload file lên dịch vụ lưu trữ.
