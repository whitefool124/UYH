from __future__ import annotations

import os
import re
import shutil
import tempfile
import zipfile
from pathlib import Path
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[2]
PAPER = ROOT / "论文材料"
TEMPLATE = ROOT / "4 软工中外合作专业--毕业设计说明书(论文) 模板.docx"
SOURCE = PAPER / "曹逸天-毕业设计说明书-最终整合版.docx"
OUT = PAPER / "曹逸天-毕业设计说明书-学校模板样式版.docx"

W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
NS = {"w": W}
ET.register_namespace("w", W)


def q(tag: str) -> str:
    return f"{{{W}}}{tag}"


def unzip_docx(src: Path, dst: Path) -> None:
    with zipfile.ZipFile(src) as z:
        z.extractall(dst)


def zip_docx(src_dir: Path, dst: Path) -> None:
    if dst.exists():
        dst.unlink()
    with zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED) as z:
        for file in src_dir.rglob("*"):
            if file.is_file():
                z.write(file, file.relative_to(src_dir).as_posix())


def para_text(p: ET.Element) -> str:
    return "".join(t.text or "" for t in p.findall(".//w:t", NS)).strip()


def set_para_text(p: ET.Element, text: str) -> None:
    runs = p.findall("w:r", NS)
    if not runs:
        runs = [ET.SubElement(p, q("r"))]
    first = runs[0]
    for child in list(first):
        first.remove(child)
    t = ET.SubElement(first, q("t"))
    t.text = text
    if text.startswith(" ") or text.endswith(" "):
        t.set("{http://www.w3.org/XML/1998/namespace}space", "preserve")
    for r in runs[1:]:
        p.remove(r)


def ensure_ppr(p: ET.Element) -> ET.Element:
    ppr = p.find("w:pPr", NS)
    if ppr is None:
        ppr = ET.Element(q("pPr"))
        p.insert(0, ppr)
    return ppr


def remove_child(parent: ET.Element, local: str) -> None:
    for child in list(parent):
        if child.tag == q(local):
            parent.remove(child)


def set_style(p: ET.Element, style_id: str) -> None:
    ppr = ensure_ppr(p)
    remove_child(ppr, "pStyle")
    st = ET.Element(q("pStyle"))
    st.set(q("val"), style_id)
    ppr.insert(0, st)


def set_jc(p: ET.Element, val: str) -> None:
    ppr = ensure_ppr(p)
    remove_child(ppr, "jc")
    jc = ET.SubElement(ppr, q("jc"))
    jc.set(q("val"), val)


def set_body_indent(p: ET.Element, first_line: str = "420") -> None:
    ppr = ensure_ppr(p)
    remove_child(ppr, "ind")
    ind = ET.SubElement(ppr, q("ind"))
    ind.set(q("firstLine"), first_line)


def clear_body_indent(p: ET.Element) -> None:
    ppr = ensure_ppr(p)
    remove_child(ppr, "ind")


def set_run_font_size(p: ET.Element, size_half_points: str, bold: bool | None = None) -> None:
    for r in p.findall("w:r", NS):
        rpr = r.find("w:rPr", NS)
        if rpr is None:
            rpr = ET.Element(q("rPr"))
            r.insert(0, rpr)
        remove_child(rpr, "sz")
        remove_child(rpr, "szCs")
        sz = ET.SubElement(rpr, q("sz"))
        sz.set(q("val"), size_half_points)
        szcs = ET.SubElement(rpr, q("szCs"))
        szcs.set(q("val"), size_half_points)
        if bold is not None:
            remove_child(rpr, "b")
            remove_child(rpr, "bCs")
            if bold:
                ET.SubElement(rpr, q("b"))
                ET.SubElement(rpr, q("bCs"))


def is_chapter(text: str) -> bool:
    return (
        re.match(r"^第\s*[0-9一二三四五六七八九十]+\s*章", text) is not None
        or text in {"摘要", "摘  要", "Abstract", "ABSTRACT", "参考文献", "图表证据补充页", "图附录", "表附录", "致谢"}
    )


def is_section(text: str) -> bool:
    return re.match(r"^[0-9]+\.[0-9]+\s+", text) is not None


def is_subsection(text: str) -> bool:
    return re.match(r"^[0-9]+\.[0-9]+\.[0-9]+\s+", text) is not None


def set_page_setup(root: ET.Element) -> None:
    for sect in root.findall(".//w:sectPr", NS):
        pg_mar = sect.find("w:pgMar", NS)
        if pg_mar is None:
            pg_mar = ET.SubElement(sect, q("pgMar"))
        pg_mar.set(q("top"), "1440")
        pg_mar.set(q("bottom"), "1440")
        pg_mar.set(q("left"), "1800")
        pg_mar.set(q("right"), "1800")
        pg_mar.set(q("header"), "851")
        pg_mar.set(q("footer"), "992")


def body_text(node: ET.Element) -> str:
    return "".join(t.text or "" for t in node.findall(".//w:t", NS)).strip()


def find_body_child_index_by_text(children: list[ET.Element], candidates: set[str]) -> int:
    normalized = {c.replace(" ", "") for c in candidates}
    for idx, child in enumerate(children):
        if child.tag != q("p"):
            continue
        text = body_text(child)
        if text.replace(" ", "") in normalized:
            return idx
    raise RuntimeError(f"Could not find any of: {sorted(candidates)}")


def template_cover_nodes(tmpl_doc_xml: Path) -> list[ET.Element]:
    tmpl_tree = ET.parse(tmpl_doc_xml)
    tmpl_root = tmpl_tree.getroot()
    tmpl_body = tmpl_root.find("w:body", NS)
    if tmpl_body is None:
        raise RuntimeError("Missing template body")

    tmpl_children = list(tmpl_body)
    cover_end = find_body_child_index_by_text(tmpl_children, {"摘要", "摘  要"})
    cover_nodes = [ET.fromstring(ET.tostring(child, encoding="utf-8")) for child in tmpl_children[:cover_end]]

    replacements = {
        "本科毕业设计说明书（论文）": "本科毕业设计说明书（论文）",
        "Undergraduate International Students’ Graduation Project Report (Thesis)": "Undergraduate International Students’ Graduation Project Report (Thesis)",
        "基于心电图图像分析的心肌梗死筛查研究与实现": "面向体感游戏的动态手势轨迹跟踪方法实现",
        "THE RESEARCH AND IMPLEMENTATION OF THE MYOCARDIAL INFARCTION SCREENING BASED ON ECG IMAGE ANALYSIS": "IMPLEMENTATION OF DYNAMIC GESTURE TRAJECTORY TRACKING METHOD FOR MOTION-SENSING GAMES",
        "学    院： 计算机科学与技术学院、软件学院": "学    院： 计算机科学与技术学院、软件学院",
        "专    业：   软件工程（中外合作办学）": "专    业：   软件工程（中外合作办学）",
        "班    级： 2020软件工程（中外合作办学）01": "班    级： 2022软件工程（中外合作办学）",
        "学    号：": "学    号： 202203340102",
        "学生姓名：": "学生姓名： 曹逸天",
        "指导老师：": "指导老师：",
        "提交日期：      2026年6月": "提交日期：      2026年6月",
    }
    for node in cover_nodes:
        if node.tag != q("p"):
            continue
        ppr = node.find("w:pPr", NS)
        if ppr is not None:
            remove_child(ppr, "sectPr")
        text = body_text(node)
        if text in replacements:
            set_para_text(node, replacements[text])
    return cover_nodes


def main() -> None:
    with tempfile.TemporaryDirectory() as td:
        work = Path(td) / "doc"
        tmpl = Path(td) / "tmpl"
        unzip_docx(SOURCE, work)
        unzip_docx(TEMPLATE, tmpl)

        # Reuse the school's style definitions directly.
        shutil.copy2(tmpl / "word" / "styles.xml", work / "word" / "styles.xml")

        doc_xml = work / "word" / "document.xml"
        tree = ET.parse(doc_xml)
        root = tree.getroot()
        body = root.find("w:body", NS)
        if body is None:
            raise RuntimeError("Missing document body")

        children = list(body)
        content_start = find_body_child_index_by_text(children, {"摘要", "摘  要"})
        cover_nodes = template_cover_nodes(tmpl / "word" / "document.xml")
        for child in children[:content_start]:
            body.remove(child)
        for idx, node in enumerate(cover_nodes):
            body.insert(idx, node)

        paragraphs = body.findall(".//w:p", NS)
        for i, p in enumerate(paragraphs):
            text = para_text(p)
            if not text:
                continue
            if i < len(cover_nodes):
                continue
            if is_chapter(text):
                set_style(p, "2")  # heading 1 in the school template.
                set_jc(p, "center")
                clear_body_indent(p)
            elif is_subsection(text):
                set_style(p, "4")  # heading 3.
                set_jc(p, "left")
                clear_body_indent(p)
            elif is_section(text):
                set_style(p, "3")  # heading 2.
                set_jc(p, "left")
                clear_body_indent(p)
            else:
                set_style(p, "1")  # Normal.
                set_jc(p, "both")
                set_body_indent(p)

        set_page_setup(root)
        tree.write(doc_xml, encoding="utf-8", xml_declaration=True)
        zip_docx(work, OUT)
        print(OUT)


if __name__ == "__main__":
    main()
