@baseUrl = https://localhost:7001
@adminToken = <adminAccessToken>
@landlordToken = <landlordAccessToken>
@tenantToken = <tenantAccessToken>
@propertyId = <property-guid>

### 1. Admin xem danh sách tin chờ duyệt (mặc định Pending)
GET {{baseUrl}}/api/admin/properties?page=1&pageSize=10
Authorization: Bearer {{adminToken}}

### 2. Admin lọc danh sách theo trạng thái kiểm duyệt
GET {{baseUrl}}/api/admin/properties?status=Rejected&page=1&pageSize=10
Authorization: Bearer {{adminToken}}

### 3. Admin xem chi tiết tin, hình ảnh, tiện ích và lịch sử kiểm duyệt
GET {{baseUrl}}/api/admin/properties/{{propertyId}}
Authorization: Bearer {{adminToken}}

### 4. Admin duyệt tin Pending
POST {{baseUrl}}/api/admin/properties/{{propertyId}}/approve
Authorization: Bearer {{adminToken}}

### 5. Admin từ chối tin Pending (bắt buộc lý do)
POST {{baseUrl}}/api/admin/properties/{{propertyId}}/reject
Authorization: Bearer {{adminToken}}
Content-Type: application/json

{
  "reason": "Thông tin địa chỉ chưa đầy đủ, vui lòng bổ sung."
}

### 6. Admin ẩn tin đã duyệt và Published (bắt buộc lý do)
POST {{baseUrl}}/api/admin/properties/{{propertyId}}/hide
Authorization: Bearer {{adminToken}}
Content-Type: application/json

{
  "reason": "Tin đăng cần được rà soát lại theo phản ánh."
}

### 7. Admin khôi phục tin đã duyệt và Hidden (bắt buộc lý do)
POST {{baseUrl}}/api/admin/properties/{{propertyId}}/restore
Authorization: Bearer {{adminToken}}
Content-Type: application/json

{
  "reason": "Đã hoàn tất rà soát, tin đăng đủ điều kiện hiển thị."
}

### 8. Landlord không được truy cập danh sách kiểm duyệt
GET {{baseUrl}}/api/admin/properties
Authorization: Bearer {{landlordToken}}

### 9. Tenant không được truy cập danh sách kiểm duyệt
GET {{baseUrl}}/api/admin/properties
Authorization: Bearer {{tenantToken}}
