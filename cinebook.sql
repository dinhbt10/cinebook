CREATE TABLE "AuditLogs" (
    "Id" uuid NOT NULL,
    "ActorUserId" uuid,
    "Action" text NOT NULL,
    "EntityType" text NOT NULL,
    "EntityId" text,
    "DataBefore" text,
    "DataAfter" text,
    "Note" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
);


CREATE TABLE "Cinemas" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "City" text NOT NULL,
    "District" text,
    "Address" text NOT NULL,
    "Hotline" text,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Cinemas" PRIMARY KEY ("Id")
);


CREATE TABLE "Combos" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text,
    "Price" numeric(12,2) NOT NULL,
    "ImageUrl" text,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Combos" PRIMARY KEY ("Id")
);


CREATE TABLE "Genres" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Slug" text NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Genres" PRIMARY KEY ("Id")
);


CREATE TABLE "Movies" (
    "Id" uuid NOT NULL,
    "Title" character varying(256) NOT NULL,
    "OriginalTitle" text,
    "Description" text,
    "DurationMinutes" integer NOT NULL,
    "ReleaseDate" date NOT NULL,
    "EndDate" date,
    "AgeRating" integer NOT NULL,
    "Director" text,
    "Cast" text,
    "Language" text,
    "Country" text,
    "PosterUrl" text,
    "BackdropUrl" text,
    "TrailerUrl" text,
    "Status" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Movies" PRIMARY KEY ("Id")
);


CREATE TABLE "PriceRules" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "CinemaId" uuid,
    "RoomType" integer,
    "SeatType" integer,
    "DayOfWeek" integer,
    "TimeFrom" time without time zone,
    "TimeTo" time without time zone,
    "Multiplier" numeric(6,3) NOT NULL,
    "Surcharge" numeric(12,2) NOT NULL,
    "Priority" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_PriceRules" PRIMARY KEY ("Id")
);


CREATE TABLE "Promotions" (
    "Id" uuid NOT NULL,
    "Code" character varying(32) NOT NULL,
    "Name" text NOT NULL,
    "DiscountType" integer NOT NULL,
    "Value" numeric(12,2) NOT NULL,
    "MaxDiscount" numeric(12,2),
    "MinOrderAmount" numeric(12,2) NOT NULL,
    "StartsAtUtc" timestamp with time zone NOT NULL,
    "EndsAtUtc" timestamp with time zone NOT NULL,
    "UsageLimit" integer,
    "UsedCount" integer NOT NULL,
    "PerUserLimit" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Promotions" PRIMARY KEY ("Id")
);


CREATE TABLE "Rooms" (
    "Id" uuid NOT NULL,
    "CinemaId" uuid NOT NULL,
    "Name" text NOT NULL,
    "RoomType" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Rooms" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Rooms_Cinemas_CinemaId" FOREIGN KEY ("CinemaId") REFERENCES "Cinemas" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Users" (
    "Id" uuid NOT NULL,
    "Email" character varying(256) NOT NULL,
    "PasswordHash" text NOT NULL,
    "FullName" character varying(128) NOT NULL,
    "Phone" character varying(20),
    "BirthDate" date,
    "Role" integer NOT NULL,
    "EmailVerified" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "CinemaId" uuid,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Users_Cinemas_CinemaId" FOREIGN KEY ("CinemaId") REFERENCES "Cinemas" ("Id") ON DELETE SET NULL
);


CREATE TABLE "MovieGenres" (
    "MovieId" uuid NOT NULL,
    "GenreId" uuid NOT NULL,
    CONSTRAINT "PK_MovieGenres" PRIMARY KEY ("MovieId", "GenreId"),
    CONSTRAINT "FK_MovieGenres_Genres_GenreId" FOREIGN KEY ("GenreId") REFERENCES "Genres" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_MovieGenres_Movies_MovieId" FOREIGN KEY ("MovieId") REFERENCES "Movies" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Seats" (
    "Id" uuid NOT NULL,
    "RoomId" uuid NOT NULL,
    "RowLabel" character varying(4) NOT NULL,
    "Number" integer NOT NULL,
    "SeatType" integer NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Seats" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Seats_Rooms_RoomId" FOREIGN KEY ("RoomId") REFERENCES "Rooms" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Showtimes" (
    "Id" uuid NOT NULL,
    "MovieId" uuid NOT NULL,
    "RoomId" uuid NOT NULL,
    "StartsAtUtc" timestamp with time zone NOT NULL,
    "EndsAtUtc" timestamp with time zone NOT NULL,
    "BasePrice" numeric(12,2) NOT NULL,
    "SubtitleMode" integer NOT NULL,
    "IsOnSale" boolean NOT NULL,
    "IsCancelled" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Showtimes" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Showtimes_Movies_MovieId" FOREIGN KEY ("MovieId") REFERENCES "Movies" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Showtimes_Rooms_RoomId" FOREIGN KEY ("RoomId") REFERENCES "Rooms" ("Id") ON DELETE CASCADE
);


CREATE TABLE "SavedVouchers" (
    "UserId" uuid NOT NULL,
    "PromotionId" uuid NOT NULL,
    "SavedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SavedVouchers" PRIMARY KEY ("UserId", "PromotionId"),
    CONSTRAINT "FK_SavedVouchers_Promotions_PromotionId" FOREIGN KEY ("PromotionId") REFERENCES "Promotions" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_SavedVouchers_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Bookings" (
    "Id" uuid NOT NULL,
    "Code" character varying(16) NOT NULL,
    "UserId" uuid NOT NULL,
    "ShowtimeId" uuid NOT NULL,
    "Status" integer NOT NULL,
    "SeatsSubtotal" numeric(12,2) NOT NULL,
    "ConcessionSubtotal" numeric(12,2) NOT NULL,
    "DiscountAmount" numeric(12,2) NOT NULL,
    "Total" numeric(12,2) NOT NULL,
    "PromotionId" uuid,
    "HoldExpiresAtUtc" timestamp with time zone NOT NULL,
    "PaidAtUtc" timestamp with time zone,
    "CancelledAtUtc" timestamp with time zone,
    "CancelReason" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Bookings" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Bookings_Promotions_PromotionId" FOREIGN KEY ("PromotionId") REFERENCES "Promotions" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Bookings_Showtimes_ShowtimeId" FOREIGN KEY ("ShowtimeId") REFERENCES "Showtimes" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Bookings_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "BookingCombos" (
    "BookingId" uuid NOT NULL,
    "ComboId" uuid NOT NULL,
    "Quantity" integer NOT NULL,
    "UnitPrice" numeric(12,2) NOT NULL,
    CONSTRAINT "PK_BookingCombos" PRIMARY KEY ("BookingId", "ComboId"),
    CONSTRAINT "FK_BookingCombos_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_BookingCombos_Combos_ComboId" FOREIGN KEY ("ComboId") REFERENCES "Combos" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Payments" (
    "Id" uuid NOT NULL,
    "BookingId" uuid NOT NULL,
    "Provider" character varying(32) NOT NULL,
    "ProviderTxnId" character varying(128) NOT NULL,
    "Amount" numeric(12,2) NOT NULL,
    "Status" integer NOT NULL,
    "RawPayload" text,
    "CompletedAtUtc" timestamp with time zone,
    "RefundedAmount" numeric(12,2) NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Payments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Payments_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("Id") ON DELETE CASCADE
);


CREATE TABLE "ShowtimeSeats" (
    "ShowtimeId" uuid NOT NULL,
    "SeatId" uuid NOT NULL,
    "Status" integer NOT NULL,
    "HeldByUserId" uuid,
    "HoldExpiresAtUtc" timestamp with time zone,
    "BookingId" uuid,
    "Price" numeric(12,2) NOT NULL,
    CONSTRAINT "PK_ShowtimeSeats" PRIMARY KEY ("ShowtimeId", "SeatId"),
    CONSTRAINT "FK_ShowtimeSeats_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_ShowtimeSeats_Seats_SeatId" FOREIGN KEY ("SeatId") REFERENCES "Seats" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_ShowtimeSeats_Showtimes_ShowtimeId" FOREIGN KEY ("ShowtimeId") REFERENCES "Showtimes" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Tickets" (
    "Id" uuid NOT NULL,
    "BookingId" uuid NOT NULL,
    "SeatId" uuid NOT NULL,
    "Code" character varying(16) NOT NULL,
    "QrSignature" text NOT NULL,
    "Status" integer NOT NULL,
    "CheckedInAtUtc" timestamp with time zone,
    "CheckedInByUserId" uuid,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "PK_Tickets" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Tickets_Bookings_BookingId" FOREIGN KEY ("BookingId") REFERENCES "Bookings" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Tickets_Seats_SeatId" FOREIGN KEY ("SeatId") REFERENCES "Seats" ("Id") ON DELETE RESTRICT
);


CREATE INDEX "IX_AuditLogs_EntityType_EntityId" ON "AuditLogs" ("EntityType", "EntityId");


CREATE INDEX "IX_BookingCombos_ComboId" ON "BookingCombos" ("ComboId");


CREATE UNIQUE INDEX "IX_Bookings_Code" ON "Bookings" ("Code");


CREATE INDEX "IX_Bookings_PromotionId" ON "Bookings" ("PromotionId");


CREATE INDEX "IX_Bookings_ShowtimeId" ON "Bookings" ("ShowtimeId");


CREATE INDEX "IX_Bookings_Status_HoldExpiresAtUtc" ON "Bookings" ("Status", "HoldExpiresAtUtc");


CREATE INDEX "IX_Bookings_UserId_CreatedAtUtc" ON "Bookings" ("UserId", "CreatedAtUtc");


CREATE UNIQUE INDEX "IX_Genres_Slug" ON "Genres" ("Slug");


CREATE INDEX "IX_MovieGenres_GenreId" ON "MovieGenres" ("GenreId");


CREATE INDEX "IX_Movies_Status" ON "Movies" ("Status");


CREATE INDEX "IX_Payments_BookingId" ON "Payments" ("BookingId");


CREATE UNIQUE INDEX "IX_Payments_Provider_ProviderTxnId" ON "Payments" ("Provider", "ProviderTxnId");


CREATE UNIQUE INDEX "IX_Promotions_Code" ON "Promotions" ("Code");


CREATE INDEX "IX_Rooms_CinemaId" ON "Rooms" ("CinemaId");


CREATE INDEX "IX_SavedVouchers_PromotionId" ON "SavedVouchers" ("PromotionId");


CREATE INDEX "IX_SavedVouchers_UserId_SavedAtUtc" ON "SavedVouchers" ("UserId", "SavedAtUtc");


CREATE UNIQUE INDEX "IX_Seats_RoomId_RowLabel_Number" ON "Seats" ("RoomId", "RowLabel", "Number");


CREATE INDEX "IX_Showtimes_MovieId_StartsAtUtc" ON "Showtimes" ("MovieId", "StartsAtUtc");


CREATE INDEX "IX_Showtimes_RoomId_StartsAtUtc" ON "Showtimes" ("RoomId", "StartsAtUtc");


CREATE INDEX "IX_ShowtimeSeats_BookingId" ON "ShowtimeSeats" ("BookingId");


CREATE INDEX "IX_ShowtimeSeats_HoldExpiresAtUtc" ON "ShowtimeSeats" ("HoldExpiresAtUtc");


CREATE INDEX "IX_ShowtimeSeats_SeatId" ON "ShowtimeSeats" ("SeatId");


CREATE INDEX "IX_ShowtimeSeats_ShowtimeId_Status" ON "ShowtimeSeats" ("ShowtimeId", "Status");


CREATE INDEX "IX_Tickets_BookingId" ON "Tickets" ("BookingId");


CREATE UNIQUE INDEX "IX_Tickets_Code" ON "Tickets" ("Code");


CREATE INDEX "IX_Tickets_SeatId" ON "Tickets" ("SeatId");


CREATE INDEX "IX_Users_CinemaId" ON "Users" ("CinemaId");


CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");


