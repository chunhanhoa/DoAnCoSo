﻿using System;
using System.Collections.Generic;

namespace TiengAnh.Models;

public partial class CauHoiKt
{
    public int IdCh { get; set; }

    public string QuestionCh { get; set; } = null!;

    public string? OptionA { get; set; }

    public string? OptionB { get; set; }

    public string? OptionC { get; set; }

    public string? OptionD { get; set; }

    public string AnswerCh { get; set; } = null!;

    public string? ExplanationCh { get; set; } // Đổi tên từ ExplanationBt thành ExplanationCh

    public int IdKt { get; set; }

    public virtual ICollection<ChiTietKetQua> ChiTietKetQuas { get; set; } = new List<ChiTietKetQua>();

    public virtual KiemTra IdKtNavigation { get; set; } = null!;
}
