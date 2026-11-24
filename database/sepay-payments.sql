-- Chạy script này trên SQL Server trước khi bật API thanh toán.
-- Không lưu Secret Key trong database hoặc source code.

CREATE TABLE dbo.Payments
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Payments PRIMARY KEY,
    InvoiceNumber NVARCHAR(40) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    Currency NVARCHAR(3) NOT NULL CONSTRAINT DF_Payments_Currency DEFAULT 'VND',
    Description NVARCHAR(255) NOT NULL,
    Status NVARCHAR(20) NOT NULL,
    CustomerId NVARCHAR(100) NULL,
    ProviderTransactionId NVARCHAR(100) NULL,
    PaymentMethod NVARCHAR(50) NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    PaidAt DATETIMEOFFSET NULL
);

CREATE UNIQUE INDEX UX_Payments_InvoiceNumber ON dbo.Payments(InvoiceNumber);
CREATE UNIQUE INDEX UX_Payments_ProviderTransactionId
    ON dbo.Payments(ProviderTransactionId)
    WHERE ProviderTransactionId IS NOT NULL;

CREATE TABLE dbo.WebhookEvents
(
    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WebhookEvents PRIMARY KEY,
    EventKey NVARCHAR(120) NOT NULL,
    Source NVARCHAR(30) NOT NULL,
    RawPayload NVARCHAR(MAX) NOT NULL,
    ReceivedAt DATETIMEOFFSET NOT NULL
);

CREATE UNIQUE INDEX UX_WebhookEvents_EventKey ON dbo.WebhookEvents(EventKey);
