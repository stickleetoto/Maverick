"""Extract the WP-3D stability dataset from a Unity batch log.

The dataset is produced by MavF15ResearchStabilityValidation.ExportDataset, run through the FDM
batch adapter:

    Unity.exe -batchmode -quit -nographics -projectPath <project> \
      -executeMethod MaverickFresh.FlightDynamics.EditorTools.MavFdmValidationBatchAdapter.RunBatch \
      -fdmSuite f15-stability-export -fdmMode sync \
      -fdmType MaverickFresh.FlightDynamics.Validation.MavF15ResearchStabilityValidation \
      -fdmMethod ExportDataset -fdmOut <result.json> -logFile <export.log>

The adapter logs the method's return value after FDM_VALIDATION_REPORT; each CSV is delimited by
BEGIN_CSV <name> / END_CSV <name>. Usage:

    python extract_from_log.py <export.log> [output_dir]
"""
import io
import os
import re
import sys


def main():
    log = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.dirname(os.path.abspath(__file__))
    text = io.open(log, encoding='utf-8', errors='replace').read()
    start = text.find('FDM_VALIDATION_REPORT')
    if start < 0:
        raise SystemExit('no FDM_VALIDATION_REPORT in ' + log)
    blocks = re.findall(r'BEGIN_CSV (\S+)\n(.*?)END_CSV \1\n', text[start:], re.S)
    if not blocks:
        raise SystemExit('no CSV blocks found')
    for name, body in blocks:
        io.open(os.path.join(out, name), 'w', encoding='utf-8', newline='\n').write(body)
        print('%s: %d lines' % (name, body.count('\n')))


if __name__ == '__main__':
    main()
