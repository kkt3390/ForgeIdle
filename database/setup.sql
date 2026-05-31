IF DB_ID(N'forgeidle') IS NULL
BEGIN
    CREATE DATABASE forgeidle
    ON PRIMARY
    (
        NAME = N'forgeidle',
        FILENAME = N'D:\SqlData\forgeidle.mdf'
    )
    LOG ON
    (
        NAME = N'forgeidle_log',
        FILENAME = N'D:\SqlData\forgeidle_log.ldf'
    );
END;
