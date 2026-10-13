public enum GameColorType {
    White = 0,          // Trắng
    Green = 1,          // Xanh lục
    Cyan = 2,           // Xanh da trời
    Blue = 3,           // Xanh dương
    Brown = 4,          // Nâu
    Purple = 5,         // Tím
    Powder = 6,         // Cam phấn / Da
    Yellow = 7,         // Vàng
    Red = 8,            // Đỏ
    EmeraldGreen = 9,   // Xanh nõn chuối
    Orange = 10,        // Cam đậm
    Black = 11,         // Xám đen
    DarkGreen = 12,     // Xanh lá đậm
    Burgundy = 13,      // Đỏ mận
    GrayishBlue = 14,   // Xanh ghi
    LightPurple = 15,   // Tím nhạt
    Pink = 16,          // Hồng phấn
    Teal = 17           // Xanh bạc hà
}

public enum BoosterType
{
    MAGNET,
    SUFFLE,
    VACCUM,
    NONE
}

/// <summary>
/// Các cơ chế đặc biệt của xe buýt trong Level (cell bus: "{color}_{capacity}_{mechanic}")
/// Ánh xạ chuẩn từ dữ liệu levelNCXHCfg.json và guideNCXHCfg.json của Cocos gốc
/// </summary>
public enum CarMechanicType
{
    Normal = 0,           // Xe tiêu chuẩn thông thường
    Linked = 1,           // Xe nối liền (Connection / Linked Car)
    Locked = 2,           // Xe bị khóa (Lock Car)
    Mystery = 3,          // Xe bí ẩn / giấu màu (Mystery Car)
    Frozen = 4,           // Xe đóng băng (Ice Car)
    Crate = 5,            // Xe trong thùng gỗ (Box / Crate Car)
    Special = 6,          // Cơ chế nâng cao

    // Các biến thể mã 1xx (Variant) trong Level Data
    LinkedVariant = 101,  // Biến thể Xe nối (Cocos guide id: 101)
    LockedVariant = 102,  // Biến thể Xe khóa xích (Cocos guide id: 102)
    MysteryVariant = 103, // Biến thể Xe bí ẩn (Cocos guide id: 103)
    FrozenVariant = 104,  // Biến thể Xe đóng băng (Cocos guide id: 104)
    CrateVariant = 105,   // Biến thể Xe thùng gỗ (Cocos guide id: 105)

    KeyCar = 501          // Xe mang chìa khóa (Key Car - mở khóa cho xe 102)
}

/// <summary>
/// Loại đường trên (Road Top / Track Type) - Trường "road" trong levelNCXHCfg.json
/// Nạp Prefab "roadTop{road}" từ Assets/GameConfig/roadNCXHCfg.json
/// </summary>
public enum RoadTopType
{
    StandardLoop_1 = 1,   // Tuyến chữ U tiêu chuẩn 1
    StandardLoop_2 = 2,   // Tuyến chữ U tiêu chuẩn 2
    StandardLoop_3 = 3,   // Tuyến chữ U tiêu chuẩn 3
    StandardLoop_4 = 4,   // Tuyến chữ U tiêu chuẩn 4
    StandardLoop_5 = 5,   // Tuyến chữ U tiêu chuẩn 5 (Mặc định)
    StandardLoop_6 = 6,   // Tuyến chữ U tiêu chuẩn 6
    StandardLoop_7 = 7,   // Tuyến chữ U tiêu chuẩn 7
    StandardLoop_8 = 8,   // Tuyến chữ U tiêu chuẩn 8
    StandardLoop_9 = 9,   // Tuyến chữ U tiêu chuẩn 9

    Bridge_11 = 11,       // Tuyến Cầu vượt 1 (Xe chạy trên cầu, khách đi dưới cầu)
    Bridge_12 = 12,       // Tuyến Cầu vượt 2
    Bridge_13 = 13,       // Tuyến Cầu vượt 3

    TunnelCave_21 = 21,   // Tuyến Hầm xuyên núi 1 (Có Cave Path chui qua hầm)
    TunnelCave_22 = 22,   // Tuyến Hầm xuyên núi 2

    Complex_31 = 31,      // Tuyến phức hợp 1
    Complex_32 = 32       // Tuyến phức hợp 2
}

/// <summary>
/// Loại môi trường / Chủ đề của Level (Environment / Theme) - Ánh xạ từ trường "bgStyle" (1..5) trong levelNCXHCfg.json
/// Khớp 1:1 với 5 thư mục tài nguyên trong Assets/Sprite/UI/level: FARM, CITY, SNOW, Sea, DESERT
/// </summary>
public enum LevelEnvironmentType
{
    Farm = 1,     // Thư mục FARM (Mục trường / Nông trại) - bgStyle = 1
    City = 2,     // Thư mục CITY (Bến xe / Đô thị) - bgStyle = 2
    Snow = 3,     // Thư mục SNOW (Băng tuyết) - bgStyle = 3
    Sea = 4,      // Thư mục Sea (Biển / Đại dương) - bgStyle = 4
    Desert = 5    // Thư mục DESERT (Sa mạc) - bgStyle = 5
}

/// <summary>
/// Loại nền đáy đường (Road Bottom / Background Style) - Trường "bgStyle" trong levelNCXHCfg.json
/// Nạp Prefab "roadBottom" theo theme bgStyle và gán màu ROAD_BOTTOM_COLOR_DEFINE[bgStyle - 1]
/// </summary>
public enum RoadBottomStyle
{
    Theme_Farm_1 = 1,        // FARM (bgStyle = 1)
    Theme_City_2 = 2,        // CITY (bgStyle = 2)
    Theme_Snow_3 = 3,        // SNOW (bgStyle = 3)
    Theme_Sea_4 = 4,         // Sea (bgStyle = 4)
    Theme_Desert_5 = 5,      // DESERT (bgStyle = 5)

    // Aliases tương thích ngược
    Theme_Grassland_1 = 1,
    Theme_DesertSand_2 = 2,
    Theme_CityStreet_3 = 3,
    Theme_SnowIce_4 = 4,
    Theme_Sea_5 = 5
}

/// <summary>
/// Phong cách giao diện UI - Trường "uiStyle" trong levelNCXHCfg.json
/// </summary>
public enum GameUIStyle
{
    Default_1 = 1,
    Special_2 = 2
}

/// <summary>
/// Hiệu ứng thời tiết màn chơi - Trường "weather" trong levelNCXHCfg.json
/// </summary>
public enum GameWeatherType
{
    Clear = 0,   // Trời trong / Nắng
    Rain = 1,    // Mưa
    Snow = 2     // Tuyết rơi
}