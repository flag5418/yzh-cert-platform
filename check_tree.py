import sys,json
d=json.load(sys.stdin)
data=d.get('data',d.get('Data',[]))
for org in data:
    if '尚龙' in org.get('label',''):
        for std in org.get('children',[]):
            if '13485' in std.get('label',''):
                print(f'标准: {std.get("label")}')
                for phase in std.get('children',[]):
                    print(f'  阶段: {phase.get("label")} (code: {phase.get("phaseCode")})')
