/* 몬스터 도감 이미지를 등급별 3장 구조에서 사냥터/순번별 1장 공용 구조로 보정합니다. */
UPDATE dbo.ea_monster_catalog
SET ImagePath = CONCAT(
        N'Content/monsters/area-',
        RIGHT(CONCAT(N'00', AreaId), 2),
        N'-',
        RIGHT(CONCAT(N'00', SlotNumber), 2),
        N'.webp'
    ),
    SortOrder = AreaId * 1000
        + SlotNumber * 10
        + CASE Grade
            WHEN N'normal' THEN 0
            WHEN N'elite' THEN 1
            WHEN N'golden' THEN 2
            ELSE 9
          END,
    UpdatedAt = SYSDATETIMEOFFSET();

SELECT AreaId, SlotNumber, Grade, MonsterKey, ImagePath, SortOrder
FROM dbo.ea_monster_catalog
ORDER BY AreaId, SlotNumber,
    CASE Grade WHEN N'normal' THEN 0 WHEN N'elite' THEN 1 WHEN N'golden' THEN 2 ELSE 9 END;
