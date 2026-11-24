/*
    TAO QUAN TRI VIEN DAU TIEN
    1. Tao mot tai khoan thong thuong qua POST /api/auth/register.
    2. Thay email ben duoi bang email tai khoan do.
    3. Chay script nay mot lan trong SSMS tren database Web2_ThueNhaDb.
    Khong tao endpoint cong khai de tu cap quyen Admin.
*/
USE [Web2_ThueNhaDb];
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Email NVARCHAR(256) = N'thay-email-cua-ban@example.com';
DECLARE @UserId UNIQUEIDENTIFIER;
DECLARE @AdminRoleId INT;

SELECT @UserId = [Id]
FROM [dbo].[Users]
WHERE LOWER([Email]) = LOWER(@Email);

IF @UserId IS NULL
    THROW 51001, N'Khong tim thay tai khoan. Hay dang ky tai khoan truoc.', 1;

SELECT @AdminRoleId = [Id]
FROM [dbo].[Roles]
WHERE [Name] = N'Admin';

IF @AdminRoleId IS NULL
    THROW 51002, N'Khong tim thay vai tro Admin trong bang Roles.', 1;

IF NOT EXISTS (
    SELECT 1 FROM [dbo].[UserRoles]
    WHERE [UserId] = @UserId AND [RoleId] = @AdminRoleId
)
BEGIN
    INSERT INTO [dbo].[UserRoles] ([UserId], [RoleId])
    VALUES (@UserId, @AdminRoleId);
END;

COMMIT TRANSACTION;

SELECT u.[Id], u.[FullName], u.[Email], r.[Name] AS [Role]
FROM [dbo].[Users] u
INNER JOIN [dbo].[UserRoles] ur ON ur.[UserId] = u.[Id]
INNER JOIN [dbo].[Roles] r ON r.[Id] = ur.[RoleId]
WHERE u.[Id] = @UserId;
