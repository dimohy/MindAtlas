// Knowledge graph rendering with D3.js force simulation
window.mindAtlasGraph = {
    render: async function (jsonStr) {
        const container = document.getElementById('graph-container');
        if (!container) return;

        // Load D3 dynamically if not present
        if (!window.d3) {
            await new Promise((resolve, reject) => {
                const script = document.createElement('script');
                script.src = 'https://cdn.jsdelivr.net/npm/d3@7/dist/d3.min.js';
                script.onload = resolve;
                script.onerror = reject;
                document.head.appendChild(script);
            });
        }

        const data = JSON.parse(jsonStr);
        if (!data.nodes || data.nodes.length === 0) return;

        container.innerHTML = '';
        const width = container.clientWidth || 800;
        const height = container.clientHeight || 600;

        const svg = d3.select(container)
            .append('svg')
            .attr('width', width)
            .attr('height', height)
            .attr('viewBox', [0, 0, width, height]);

        // Color scale by group
        const color = d3.scaleOrdinal(d3.schemeTableau10);

        const simulation = d3.forceSimulation(data.nodes)
            .force('link', d3.forceLink(data.links).id(d => d.id).distance(100))
            .force('charge', d3.forceManyBody().strength(-300))
            .force('center', d3.forceCenter(width / 2, height / 2))
            .force('collision', d3.forceCollide(30));

        // Arrows
        svg.append('defs').selectAll('marker')
            .data(['arrow'])
            .join('marker')
            .attr('id', 'arrow')
            .attr('viewBox', '0 -5 10 10')
            .attr('refX', 20)
            .attr('refY', 0)
            .attr('markerWidth', 6)
            .attr('markerHeight', 6)
            .attr('orient', 'auto')
            .append('path')
            .attr('fill', '#999')
            .attr('d', 'M0,-5L10,0L0,5');

        const link = svg.append('g')
            .selectAll('line')
            .data(data.links)
            .join('line')
            .attr('stroke', '#999')
            .attr('stroke-opacity', 0.6)
            .attr('stroke-width', 1.5)
            .attr('marker-end', 'url(#arrow)');

        const node = svg.append('g')
            .selectAll('g')
            .data(data.nodes)
            .join('g')
            .call(d3.drag()
                .on('start', dragStarted)
                .on('drag', dragged)
                .on('end', dragEnded));

        node.append('circle')
            .attr('r', 8)
            .attr('fill', d => color(d.group))
            .attr('stroke', '#fff')
            .attr('stroke-width', 1.5);

        node.append('text')
            .attr('dx', 12)
            .attr('dy', 4)
            .attr('font-size', '11px')
            .attr('fill', 'currentColor')
            .text(d => d.id);

        // Click to navigate
        node.style('cursor', 'pointer')
            .on('click', (event, d) => {
                window.location.href = '/wiki/' + encodeURIComponent(d.id);
            });

        // Tooltip
        node.append('title').text(d => d.id);

        simulation.on('tick', () => {
            link
                .attr('x1', d => d.source.x)
                .attr('y1', d => d.source.y)
                .attr('x2', d => d.target.x)
                .attr('y2', d => d.target.y);

            node.attr('transform', d => `translate(${d.x},${d.y})`);
        });

        function dragStarted(event) {
            if (!event.active) simulation.alphaTarget(0.3).restart();
            event.subject.fx = event.subject.x;
            event.subject.fy = event.subject.y;
        }

        function dragged(event) {
            event.subject.fx = event.x;
            event.subject.fy = event.y;
        }

        function dragEnded(event) {
            if (!event.active) simulation.alphaTarget(0);
            event.subject.fx = null;
            event.subject.fy = null;
        }
    }
};
