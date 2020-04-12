﻿/*
 * HFM.NET
 * Copyright (C) 2009-2017 Ryan Harlamert (harlam357)
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; version 2
 * of the License. See the included file GPLv2.TXT.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using AutoMapper;

using HFM.Core.Client;
using HFM.Core.Data;
using HFM.Core.SlotXml;
using HFM.Core.WorkUnits;

namespace HFM.Core.Configuration
{
    [ExcludeFromCodeCoverage]
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<SlotModel, SlotData>()
               .ForMember(dest => dest.StatusColor, opt => opt.MapFrom(src => ColorTranslator.ToHtml(src.Status.GetStatusColor())))
               .ForMember(dest => dest.StatusFontColor, opt => opt.MapFrom(src => ColorTranslator.ToHtml(HtmlBuilder.GetHtmlFontColor(src.Status))))
               .ForMember(dest => dest.ETA, opt => opt.MapFrom(src => src.ShowETADate ? src.ETADate.ToStringOrUnknown() : src.ETA.ToString()))
               .ForMember(dest => dest.DownloadTime, opt => opt.MapFrom(src => src.DownloadTime.ToStringOrUnknown()))
               .ForMember(dest => dest.PreferredDeadline, opt => opt.MapFrom(src => src.PreferredDeadline.ToStringOrUnknown()))
               .ForMember(dest => dest.Protein, opt => opt.MapFrom(src => CreateMarkupProtein(src.WorkUnitModel.CurrentProtein)));

            CreateMap<Log.LogLine, LogLine>();
            CreateMap<Proteins.Protein, Protein>();

            CreateMap<WorkUnit, WorkUnitRow>()
               .ForMember(dest => dest.ID, opt => opt.Ignore())
               .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.SlotIdentifier.Name))
               .ForMember(dest => dest.Path, opt => opt.MapFrom(src => src.SlotIdentifier.Client.ToPath()))
               .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.FoldingID))
               .ForMember(dest => dest.FramesCompleted, opt => opt.Ignore())
               .ForMember(dest => dest.FrameTimeValue, opt => opt.Ignore())
               .ForMember(dest => dest.ResultValue, opt => opt.MapFrom(src => (int)src.UnitResult))
               .ForMember(dest => dest.DownloadDateTime, opt => opt.MapFrom(src => src.DownloadTime))
               .ForMember(dest => dest.CompletionDateTime, opt => opt.MapFrom(src => src.FinishedTime))
               .ForMember(dest => dest.WorkUnitName, opt => opt.Ignore())
               .ForMember(dest => dest.KFactor, opt => opt.Ignore())
               .ForMember(dest => dest.Core, opt => opt.Ignore())
               .ForMember(dest => dest.Frames, opt => opt.Ignore())
               .ForMember(dest => dest.Atoms, opt => opt.Ignore())
               .ForMember(dest => dest.BaseCredit, opt => opt.Ignore())
               .ForMember(dest => dest.PreferredDays, opt => opt.Ignore())
               .ForMember(dest => dest.MaximumDays, opt => opt.Ignore())
               .ForMember(dest => dest.SlotType, opt => opt.Ignore())
               .ForMember(dest => dest.ProductionView, opt => opt.Ignore())
               .ForMember(dest => dest.PPD, opt => opt.Ignore())
               .ForMember(dest => dest.Credit, opt => opt.Ignore());
        }

        private static Protein CreateMarkupProtein(Proteins.Protein p)
        {
            return p == null ? null : Mapper.Map<Protein>(p);
        }
    }
}
