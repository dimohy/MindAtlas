// Knowledge graph rendering with a local SVG force layout.
window.mindAtlasGraph = {
    render: function (jsonStr) {
        const container = document.getElementById('graph-container');
        if (!container) return;

        const data = JSON.parse(jsonStr);
        if (!data.nodes || data.nodes.length === 0) {
            container.innerHTML = '<div class="graph-empty">No graph data</div>';
            return;
        }

        container.innerHTML = '';
        const width = container.clientWidth || 800;
        const height = container.clientHeight || 600;
        const namespace = 'http://www.w3.org/2000/svg';
        const palette = ['#7c83ff', '#22c55e', '#f59e0b', '#ef4444', '#06b6d4', '#a855f7', '#14b8a6', '#f97316'];
        const relationshipPalette = new Map([
            ['supports', '#16a34a'],
            ['contradicts', '#dc2626'],
            ['supersedes', '#9333ea'],
            ['references', '#64748b'],
            ['causes', '#ea580c'],
            ['depends_on', '#0891b2'],
            ['prerequisite_for', '#2563eb'],
            ['blocks', '#b91c1c'],
            ['blocked_by', '#b91c1c'],
            ['fixes', '#059669'],
            ['explains', '#7c3aed'],
            ['challenges', '#e11d48'],
            ['related', '#94a3b8'],
        ]);
        const groups = new Map();

        const nodes = data.nodes.map((node, index) => {
            const angle = (Math.PI * 2 * index) / Math.max(data.nodes.length, 1);
            const radius = Math.min(width, height) * 0.32;
            if (!groups.has(node.group)) groups.set(node.group, groups.size);

            return {
                id: node.id,
                group: node.group || 'default',
                x: width / 2 + Math.cos(angle) * radius,
                y: height / 2 + Math.sin(angle) * radius,
                vx: 0,
                vy: 0,
                fixed: false,
            };
        });

        const byId = new Map(nodes.map(node => [node.id, node]));
        const links = (data.links || [])
            .map(link => ({ source: byId.get(link.source), target: byId.get(link.target), type: link.type || 'related' }))
            .filter(link => link.source && link.target);
        const activeTypes = new Set(links.map(link => link.type));

        settleLayout(nodes, links, width, height);

        const svg = document.createElementNS(namespace, 'svg');
        svg.setAttribute('width', width);
        svg.setAttribute('height', height);
        svg.setAttribute('viewBox', `0 0 ${width} ${height}`);
        svg.setAttribute('role', 'img');
        svg.setAttribute('aria-label', 'MindAtlas knowledge graph');
        container.appendChild(svg);

        const lineLayer = document.createElementNS(namespace, 'g');
        const nodeLayer = document.createElementNS(namespace, 'g');
        svg.append(lineLayer, nodeLayer);

        const linkElements = links.map(link => {
            const line = document.createElementNS(namespace, 'line');
            line.setAttribute('class', 'graph-link');
            line.setAttribute('stroke', relationshipColor(link.type));
            lineLayer.appendChild(line);

            const label = document.createElementNS(namespace, 'text');
            label.setAttribute('class', 'graph-link-label');
            label.textContent = link.type === 'related' ? '' : `@${link.type}`;
            lineLayer.appendChild(label);

            return { line, label, link };
        });

        const nodeElements = nodes.map(node => {
            const group = document.createElementNS(namespace, 'g');
            group.setAttribute('class', 'graph-node');
            group.setAttribute('tabindex', '0');

            const circle = document.createElementNS(namespace, 'circle');
            circle.setAttribute('r', String(Math.max(8, Math.min(18, 7 + degree(node, links)))));
            circle.setAttribute('fill', palette[groups.get(node.group) % palette.length]);

            const label = document.createElementNS(namespace, 'text');
            label.setAttribute('x', '14');
            label.setAttribute('y', '4');
            label.textContent = node.id;

            const title = document.createElementNS(namespace, 'title');
            title.textContent = node.id;

            group.append(circle, label, title);
            nodeLayer.appendChild(group);
            bindNodeInteraction(group, node);
            return { group, node };
        });

        createRelationshipControls();
        render();

        function render() {
            const visibleNodeIds = new Set();

            for (const item of linkElements) {
                const isVisible = activeTypes.has(item.link.type);
                item.line.style.display = isVisible ? '' : 'none';
                item.label.style.display = isVisible ? '' : 'none';

                if (isVisible) {
                    visibleNodeIds.add(item.link.source.id);
                    visibleNodeIds.add(item.link.target.id);
                }

                item.line.setAttribute('x1', item.link.source.x.toFixed(2));
                item.line.setAttribute('y1', item.link.source.y.toFixed(2));
                item.line.setAttribute('x2', item.link.target.x.toFixed(2));
                item.line.setAttribute('y2', item.link.target.y.toFixed(2));
                item.label.setAttribute('x', ((item.link.source.x + item.link.target.x) / 2).toFixed(2));
                item.label.setAttribute('y', ((item.link.source.y + item.link.target.y) / 2).toFixed(2));
            }

            for (const item of nodeElements) {
                item.group.setAttribute('transform', `translate(${item.node.x.toFixed(2)},${item.node.y.toFixed(2)})`);
                item.group.style.opacity = links.length === 0 || visibleNodeIds.has(item.node.id) ? '1' : '0.28';
            }
        }

        function createRelationshipControls() {
            const typeCounts = new Map();
            for (const link of links) {
                typeCounts.set(link.type, (typeCounts.get(link.type) || 0) + 1);
            }

            if (typeCounts.size === 0) return;

            const controls = document.createElement('div');
            controls.className = 'graph-controls';

            const title = document.createElement('div');
            title.className = 'graph-controls-title';
            title.textContent = 'Relationships';
            controls.appendChild(title);

            for (const type of Array.from(typeCounts.keys()).sort(compareRelationshipTypes)) {
                const label = document.createElement('label');
                label.className = 'graph-filter';

                const checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.checked = true;
                checkbox.addEventListener('change', () => {
                    if (checkbox.checked) activeTypes.add(type);
                    else activeTypes.delete(type);
                    render();
                });

                const swatch = document.createElement('span');
                swatch.className = 'graph-filter-swatch';
                swatch.style.background = relationshipColor(type);

                const text = document.createElement('span');
                text.textContent = `${type === 'related' ? type : `@${type}`} (${typeCounts.get(type)})`;

                label.append(checkbox, swatch, text);
                controls.appendChild(label);
            }

            container.appendChild(controls);
        }

        function bindNodeInteraction(element, node) {
            let dragging = false;
            let moved = false;

            element.addEventListener('pointerdown', event => {
                dragging = true;
                moved = false;
                node.fixed = true;
                element.setPointerCapture(event.pointerId);
            });

            element.addEventListener('pointermove', event => {
                if (!dragging) return;
                moved = true;
                const point = svgPoint(svg, event.clientX, event.clientY);
                node.x = clamp(point.x, 20, width - 20);
                node.y = clamp(point.y, 20, height - 20);
                render();
            });

            element.addEventListener('pointerup', event => {
                dragging = false;
                node.fixed = false;
                element.releasePointerCapture(event.pointerId);
                if (!moved) window.location.href = '/wiki/' + encodeURIComponent(node.id);
            });

            element.addEventListener('keydown', event => {
                if (event.key === 'Enter') window.location.href = '/wiki/' + encodeURIComponent(node.id);
            });
        }

        function svgPoint(svgElement, clientX, clientY) {
            const point = svgElement.createSVGPoint();
            point.x = clientX;
            point.y = clientY;
            return point.matrixTransform(svgElement.getScreenCTM().inverse());
        }

        function settleLayout(layoutNodes, layoutLinks, layoutWidth, layoutHeight) {
            for (let i = 0; i < 180; i++) {
                for (const node of layoutNodes) {
                    node.vx += (layoutWidth / 2 - node.x) * 0.0008;
                    node.vy += (layoutHeight / 2 - node.y) * 0.0008;
                }

                for (let a = 0; a < layoutNodes.length; a++) {
                    for (let b = a + 1; b < layoutNodes.length; b++) {
                        const left = layoutNodes[a];
                        const right = layoutNodes[b];
                        const dx = right.x - left.x || 0.01;
                        const dy = right.y - left.y || 0.01;
                        const distanceSquared = dx * dx + dy * dy;
                        const force = Math.min(3.5, 1800 / distanceSquared);
                        const distance = Math.sqrt(distanceSquared);
                        const fx = (dx / distance) * force;
                        const fy = (dy / distance) * force;
                        left.vx -= fx;
                        left.vy -= fy;
                        right.vx += fx;
                        right.vy += fy;
                    }
                }

                for (const link of layoutLinks) {
                    const dx = link.target.x - link.source.x;
                    const dy = link.target.y - link.source.y;
                    const distance = Math.sqrt(dx * dx + dy * dy) || 1;
                    const force = (distance - 120) * 0.012;
                    const fx = (dx / distance) * force;
                    const fy = (dy / distance) * force;
                    link.source.vx += fx;
                    link.source.vy += fy;
                    link.target.vx -= fx;
                    link.target.vy -= fy;
                }

                for (const node of layoutNodes) {
                    node.vx *= 0.82;
                    node.vy *= 0.82;
                    node.x = clamp(node.x + node.vx, 24, layoutWidth - 24);
                    node.y = clamp(node.y + node.vy, 24, layoutHeight - 24);
                }
            }
        }

        function degree(node, graphLinks) {
            return graphLinks.filter(link => link.source === node || link.target === node).length;
        }

        function relationshipColor(type) {
            return relationshipPalette.get(type) || relationshipPalette.get('related');
        }

        function compareRelationshipTypes(left, right) {
            if (left === 'related') return 1;
            if (right === 'related') return -1;
            return left.localeCompare(right);
        }

        function clamp(value, min, max) {
            return Math.max(min, Math.min(max, value));
        }
    }
};
